﻿// this file was generated by the xRM Test Framework VS Extension

#region Imports

using System;
using System.Linq;
using Yagasoft.AutoNumbering.Plugins.Helpers;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Config.Plugins
{
	/// <summary>
	///     This plugin ... .<br />
	///     Version: 0.1.1
	/// </summary>
	public class PostUpdateConfigTriggerId : IPlugin
	{
		public void Execute(IServiceProvider serviceProvider)
		{
			////var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
			new PostUpdateConfigTriggerIdLogic().Execute(this, serviceProvider, PluginUser.System);
		}
	}

	[Log]
	internal class PostUpdateConfigTriggerIdLogic : PluginLogic<PostUpdateConfigTriggerId>
	{
		public PostUpdateConfigTriggerIdLogic() : base("Update", PluginStage.PostOperation, AutoNumbering.EntityLogicalName)
		{ }

		[NoLog]
		protected override void ExecuteLogic()
		{
			// get the triggering record
			var target = (Entity)Context.InputParameters["Target"];

			Log.LogAttributeValues(target, target.Attributes, "Target Attributes");

			var autoNumberConfig = Context.PostEntityImages.FirstOrDefault().Value?.ToEntity<AutoNumbering>();

			if (autoNumberConfig == null)
			{
				throw new InvalidPluginExecutionException($"Must register a full post image on step.");
			}

			Log.LogAttributeValues(autoNumberConfig, autoNumberConfig.Attributes, "Post Image Attributes");

			if (string.IsNullOrEmpty(autoNumberConfig.TriggerID))
			{
				Log.LogWarning($"Trigger ID is empty.");
				return;
			}

			AllocateBacklog(autoNumberConfig);
		}

		private void AllocateBacklog(AutoNumbering config)
		{
			var triggerId = config.TriggerID;
			var index = config.CurrentIndex;
			var threshold = config.BacklogThreshold;

			var backlogEntry =
				new AutoNumberingBacklog
				{
					TriggerID = triggerId,
					AutoNumberingConfig = config.Id,
				};

			// get an old backLog, if not, then create a new one
			if (threshold.HasValue)
			{
				var queryXml =
					$@"<fetch top='1' >
  <entity name='ys_autonumberingbacklog' >
    <attribute name='ys_autonumberingbacklogid' />
    <attribute name='ys_indexvalue' />
    <attribute name='ys_triggerid' />
    <filter>
      <condition attribute='modifiedon' operator='olderthan-x-minutes' value='{threshold.Value}' />
      <condition attribute='ys_autonumberingconfigid' operator='eq' value='{config.Id}' />
    </filter>
    <order attribute='ys_indexvalue' />
  </entity>
</fetch>";
				Log.LogDebug("Query XML", queryXml);

				Log.Log($"Retrieving first old backlog entry older than {threshold.Value} ...");
				var backlogEntryTemp = Service.RetrieveMultiple(new FetchExpression(queryXml)).Entities.FirstOrDefault();
				Log.Log($"Finished retrieving first old backlog entry.");

				if (backlogEntryTemp == null)
				{
					Log.Log($"Couldn't find any old backlog entries.");

					var updatedAutoNumbering =
						new AutoNumbering
						{
							Id = config.Id
						};

					backlogEntry.IndexValue = Helper.GetNextIndex(config, updatedAutoNumbering);

					Log.Log("Incrementing auto-numbering config's index ...");
					Service.Update(updatedAutoNumbering);
					Log.Log("Finished incrementing auto-numbering config's index.");
				}
				else
				{
					backlogEntry = backlogEntryTemp.ToEntity<AutoNumberingBacklog>();
				}
			}

			backlogEntry.TriggerID = triggerId;
			backlogEntry.KeyAttributes.Add(AutoNumberingBacklog.Fields.TriggerID, triggerId);

			Log.Log($"Upserting backlog with trigger ID '{triggerId}' and index {index} ...");
			Service.Execute(
				new UpsertRequest
				{
					Target = backlogEntry
				});
			Log.Log($"Finished Upserting backLog.");
		}
	}
}
