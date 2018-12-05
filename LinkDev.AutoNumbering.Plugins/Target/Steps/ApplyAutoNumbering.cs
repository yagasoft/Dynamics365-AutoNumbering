#region Imports

using System.Activities;
using System.Linq;
using System.Text.RegularExpressions;
using LinkDev.AutoNumbering.Plugins.BLL;
using LinkDev.AutoNumbering.Plugins.Helpers;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;

#endregion

namespace LinkDev.AutoNumbering.Plugins.Target.Steps
{
	/// <summary>
	///     This workflow takes a config record and executes autonumbering on the fields in the record.<br />
	///     It should only be added to WFs set on 'after' operation.<br />
	///     Author: Ahmed el-Sawalhy<br />
	///     Version: 2.2.2
	/// </summary>
	public class ApplyAutoNumbering : CodeActivity
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
			new ApplyAutoNumberingLogic().Execute(this, executionContext);
		}
	}

	[Log]
	internal class ApplyAutoNumberingLogic : StepLogic<ApplyAutoNumbering>
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
				 select autoNumberQ).FirstOrDefault();

			log.Log("Getting target ...");
			var target = context.InputParameters.FirstOrDefault(keyVal => keyVal.Key == "Target").Value as Entity
				?? context.PostEntityImages.FirstOrDefault().Value
				?? service.Retrieve(context.PrimaryEntityName, context.PrimaryEntityId, new ColumnSet(true));

			var autoNumberConfig = Helper.PreValidation(service, target, autoNumberTest, log, false, false);

			if (autoNumberConfig == null)
			{
				log.Log("Couldn't find auto-numbering record.", LogLevel.Warning);
				return;
			}

			// to avoid problems with missing fields that are needed by the parser, fetch the whole record
			// if the format string doesn't contain an attribute reference, then skip
			if (context.MessageName != "Create" && Regex.IsMatch(autoNumberConfig.FormatString, @"{\w*?}"))
			{
				if (target == null)
				{
					throw new InvalidPluginExecutionException(
						"Couldn't find a target for the execution; make sure the action is not \"global\".");
				}

				var columns = Regex.Matches(autoNumberConfig.FormatString, @"{\w*?}").Cast<Match>()
					.Select(match => match.Value.Replace("{", "").Replace("}", "").Split('.')[0]).ToArray();

				if (target.Attributes.Keys.Intersect(columns).Count() < columns.Length)
				{
					target = service.Retrieve(target.LogicalName, target.Id, new ColumnSet(columns));
				}
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
