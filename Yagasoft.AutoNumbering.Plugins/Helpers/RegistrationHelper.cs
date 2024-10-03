#region Imports

using System;
using System.Linq;
using System.Text;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Helpers
{
	[Log]
	internal class RegistrationHelper
	{
		private readonly IOrganizationService service;
		private readonly ILogger log;
		private readonly XrmServiceContext xrmContext;

		internal RegistrationHelper(IOrganizationService service, ILogger log)
		{
			this.service = service;
			this.log = log;
			xrmContext = new XrmServiceContext(service) { MergeOption = MergeOption.NoTracking };
		}

		internal void RegisterStageConfigSteps(AutoNumbering preConfig, AutoNumbering postConfig)
		{
			var types = (from typeQ in xrmContext.PluginTypeSet
						 where typeQ.Name == "Yagasoft.AutoNumbering.Plugins.Target.Plugins.PreCreateTargetAutoNum"
							 || typeQ.Name == "Yagasoft.AutoNumbering.Plugins.Target.Plugins.PostCreateTargetAutoNum"
						 select new PluginType
								{
									PluginTypeId = typeQ.PluginTypeId,
									Name = typeQ.Name
								}).ToList();

			var entityName = (postConfig ?? preConfig)?.EntityLogicalName_ys_EntityLogicalName;
			var id = (postConfig ?? preConfig)?.UniqueID;

			entityName.RequireNotEmpty("Entity Name");
			id.RequireNotEmpty("Unique ID");

			var createMessage = GetMessage(entityName, "Create");

			if (preConfig != null)
			{
				foreach (var type in types)
				{
					createMessage.PluginTypeId = type.PluginTypeId.GetValueOrDefault();
					DeleteExistingSteps(createMessage, id);
				}
			}

			var stage = postConfig?.AutoregisterStepStage;

			if (stage == null)
			{
				log.Log("No stage defined. Exiting ...");
				return;
			}

			var stageType = types.FirstOrDefault(t =>
				stage == AutoNumbering.AutoregisterStepStageEnum.Preoperation
					? t.Name.Contains("PreCreateTargetAutoNum")
					: t.Name.Contains("PostCreateTargetAutoNum"));

			if (stageType == null)
			{
				throw new InvalidPluginExecutionException($"Couldn't find Auto-numbering plugin type for stage '{stage}'.");
			}

			createMessage.PluginTypeId = stageType.PluginTypeId.GetValueOrDefault();
			createMessage.TypeName = stageType.Name;
			createMessage.ExecutionStage = stage == AutoNumbering.AutoregisterStepStageEnum.Preoperation
				? SdkMessageProcessingStep.ExecutionStageEnum.Preoperation
				: SdkMessageProcessingStep.ExecutionStageEnum.Postoperation;

			CreateStep(createMessage, entityName, 900, id);
		}

		private SdkMessageInfo GetMessage(string entityName, string messageName)
		{
			var message =
				(from messageQ in xrmContext.SdkMessageSet
				 join filter in xrmContext.SdkMessageFilterSet
					 on messageQ.SdkMessageIdId equals filter.SDKMessageID
				 where messageQ.Name == messageName && filter.PrimaryObjectTypeCode == entityName
				 select new SdkMessageInfo
						{
							MessageId = messageQ.SdkMessageIdId.GetValueOrDefault(),
							FilteredId = filter.SdkMessageFilterIdId.GetValueOrDefault(),
							MessageName = messageQ.Name
						}).FirstOrDefault();

			if (message?.MessageId == Guid.Empty || message?.FilteredId == Guid.Empty)
			{
				throw new InvalidPluginExecutionException($"Couldn't find '{messageName}' message for entity '{entityName}'.");
			}

			return message;
		}

		private void DeleteExistingSteps(SdkMessageInfo messageInfo, string unsecureConfig)
		{
			log.Log($"Retrieving existing steps: '{messageInfo.ExecutionStage}' '{messageInfo.MessageId}'"
				+ $" '{messageInfo.FilteredId}' '{unsecureConfig}' ...");
			var existingSteps =
				(from stepQ in new XrmServiceContext(service).SdkMessageProcessingStepSet
				 where (stepQ.ExecutionStage == SdkMessageProcessingStep.ExecutionStageEnum.Preoperation
					 || stepQ.ExecutionStage == SdkMessageProcessingStep.ExecutionStageEnum.Postoperation)
					 && stepQ.SDKMessage == messageInfo.MessageId && stepQ.SdkMessageFilter == messageInfo.FilteredId
					 && stepQ.Configuration == unsecureConfig
				 select new SdkMessageProcessingStep
						{
							SdkMessageProcessingStepIdId = stepQ.Id,
							Name = stepQ.Name
						}).ToList();
			log.Log($"Retrieved {existingSteps.Count} steps.");

			foreach (var existingStep in existingSteps)
			{
				log.Log($"Deleting step '{existingStep.Id}':'{existingStep.Name}', with config '{unsecureConfig}' ...");
				service.Delete(SdkMessageProcessingStep.EntityLogicalName, existingStep.Id);
				log.Log($"Deleted step.");
			}
		}

		private Guid? CreateStep(SdkMessageInfo messageInfo, string entityName, int order,
			string unsecureConfig = null, params string[] filteringAttributes)
		{
			var stepName = GetStepName(messageInfo.TypeName, messageInfo.MessageName, entityName,
				messageInfo.ExecutionStage.ToString(), "Sync", order.ToString());

			var step =
				new SdkMessageProcessingStep
				{
					Name = stepName,
					ExecutionOrder = order,
					ExecutionStage = messageInfo.ExecutionStage,
					ExecutionMode = SdkMessageProcessingStep.ExecutionModeEnum.Synchronous,
					Deployment = SdkMessageProcessingStep.DeploymentEnum.ServerOnly,
					AsynchronousAutomaticDelete = false,
					SDKMessage = messageInfo.MessageId,
					SdkMessageFilter = messageInfo.FilteredId,
					EventHandler = new EntityReference(PluginType.EntityLogicalName, messageInfo.PluginTypeId)
				};

			if (filteringAttributes?.Any() == true)
			{
				step.FilteringAttributes = filteringAttributes.Aggregate((a1, a2) => a1 + "," + a2);
			}

			if (unsecureConfig.IsNotEmpty())
			{
				step.Configuration = unsecureConfig;
			}

			var stepId = service.Create(step);

			log.Log($"Created step '{stepName}', with unsecure config '{unsecureConfig}'.");

			return stepId;
		}

		private string GetStepName(string typeName, string messageName, string entityName,
			string stage, string mode, string order)
		{
			var builder = new StringBuilder();
			builder.Append(typeName);
			builder.Append(": ");
			builder.Append(messageName);
			builder.Append(" of ");
			builder.Append(entityName == "none" ? "any entity" : entityName);
			builder.Append(": ");
			builder.Append(stage);
			builder.Append(" ");
			builder.Append(mode);
			builder.Append(" at ");
			builder.Append(order);

			return builder.ToString();
		}

		private class SdkMessageInfo
		{
			internal Guid MessageId;
			internal Guid FilteredId;
			internal string MessageName;
			internal Guid PluginTypeId;
			internal string TypeName;
			internal SdkMessageProcessingStep.ExecutionStageEnum ExecutionStage;
		}
	}
}
