#region Imports

using System.Activities;
using System.Linq;
using System.Text.RegularExpressions;
using LinkDev.AutoNumbering.Plugins.AutoNumber.BLL;
using LinkDev.AutoNumbering.Plugins.AutoNumber.Helpers;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;

#endregion

namespace LinkDev.AutoNumbering.Plugins.AutoNumber.Core
{
	/// <summary>
	/// DEPRECATED
	/// </summary>
	public class AutoNumberStep : CodeActivity
	{
		[Input("Auto-numbering Config")]
		[ReferenceTarget("ldv_autonumbering")]
		[RequiredArgument]
		public InArgument<EntityReference> AutoNumberRef { get; set; }

		[Input("Input Parameters (Semicolon SV)")]
		public InArgument<string> InputParam { get; set; }

		[Output("Index")]
		public OutArgument<int> Index { get; set; }

		[Output("Index String")]
		public OutArgument<string> IndexString { get; set; }

		[Output("Generated String")]
		public OutArgument<string> GeneratedString { get; set; }

		protected override void Execute(CodeActivityContext executionContext)
		{
			new AutoNumberStepLogic().Execute(this, executionContext);
		}
	}

	[Log]
	internal class AutoNumberStepLogic : StepLogic<AutoNumberStep>
	{
		[NoLog]
		protected override void ExecuteLogic()
		{
			var inputParams = codeActivity.InputParam.Get(executionContext)?.Split(';').Select(param => param.Trim());
			var autoNumberId = codeActivity.AutoNumberRef.Get(executionContext).Id;

			// get it once to check for generator field update below
			log.Log("Getting auto-numbering config ...");
			var autoNumberTest =
				(from autoNumberQ in new XrmServiceContext(service).AutoNumberingSet
				 where autoNumberQ.AutoNumberingId == autoNumberId
					 && autoNumberQ.Status == AutoNumbering.StatusEnum.Active
				 select new AutoNumbering
						{
							Id = autoNumberQ.Id,
							Status = autoNumberQ.Status,
							FieldLogicalName = autoNumberQ.FieldLogicalName,
							IncrementOnUpdate = autoNumberQ.IncrementOnUpdate,
							FormatString = autoNumberQ.FormatString,
							Condition = autoNumberQ.Condition,
							EntityLogicalName_ldv_EntityLogicalName =
								autoNumberQ.EntityLogicalName_ldv_EntityLogicalName,
							Owner = autoNumberQ.Owner
						}).FirstOrDefault();

			log.Log("Getting target ...");
			var targetObject = context.InputParameters.FirstOrDefault(keyVal => keyVal.Key == "Target").Value;
			// the target might be an entity or entity reference
			var targetRef = targetObject as EntityReference;
			var target = targetObject as Entity 
				?? (targetRef != null ? new Entity(targetRef.LogicalName) { Id = targetRef.Id } : null);

			var autoNumberConfig = Helper.PreValidation(service, target, autoNumberTest, log, false, false);

			if (autoNumberConfig == null)
			{
				log.Log("Couldn't find auto-numbering record.", LogLevel.Warning);
				return;
			}

			// to avoid problems with missing fields that are needed by the parser, fetch the whole record
			// if the format string doesn't contain an attribute reference, then skip
			if (context.MessageName != "Create" && Regex.IsMatch(autoNumberConfig.FormatString, @"{\$.*?}"))
			{
				if (target == null)
				{
					throw new InvalidPluginExecutionException(
						"Couldn't find a target for the execution; make sure the action is not \"global\".");
				}

				var columns = Regex.Matches(autoNumberConfig.FormatString, @"{\$.*?}").Cast<Match>()
					.Select(match => match.Value.Replace("{", "").Replace("}", "").TrimStart('$').Split('$')[0]).ToArray();
				target = service.Retrieve(target.LogicalName, target.Id, new ColumnSet(columns));
			}

			var autoNumbering = new AutoNumberingEngine(service, log, autoNumberConfig, target, target,
				context.OrganizationId.ToString(), inputParams);
			var result = autoNumbering.GenerateAndUpdateRecord(isUpdate: context.MessageName == "Update");

			codeActivity.Index.Set(executionContext, result.Index);
			codeActivity.IndexString.Set(executionContext, result.IndexString);
			codeActivity.GeneratedString.Set(executionContext, result.GeneratedString);
		}
	}
}
