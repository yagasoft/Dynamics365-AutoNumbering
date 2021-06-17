#region Imports

using System.Activities;
using System.Linq;
using System.Text.RegularExpressions;
using Yagasoft.AutoNumbering.Plugins.BLL;
using Yagasoft.AutoNumbering.Plugins.Helpers;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Target.Steps
{
	/// <summary>
	///     This workflow takes a config record and executes autonumbering on the fields in the record.<br />
	///     It should only be added to WFs set on 'after' operation.<br />
	///     Author: Ahmed Elsawalhy<br />
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
			var inputParams = codeActivity.InputParam.Get(ExecutionContext)?.Split(';').Select(param => param.Trim());
			var autoNumberId = codeActivity.AutoNumberRef.Get(ExecutionContext).Id;

			// get it once to check for generator field update below
			Log.Log("Getting auto-numbering config ...");
			var autoNumberTest =
				(from autoNumberQ in new XrmServiceContext(Service).AutoNumberingSet
				 where autoNumberQ.AutoNumberingId == autoNumberId
					 && autoNumberQ.Status == AutoNumbering.StatusEnum.Active
				 select autoNumberQ).FirstOrDefault();

			Log.Log("Getting target ...");
			var target = Context.PostEntityImages.FirstOrDefault().Value
				?? Service.Retrieve(Context.PrimaryEntityName, Context.PrimaryEntityId, new ColumnSet(true));

			var autoNumberConfig = Helper.PreValidation(Service, target, autoNumberTest, Log, false, false);

			if (autoNumberConfig == null)
			{
				Log.Log("Couldn't find auto-numbering record.", LogLevel.Warning);
				return;
			}

			var autoNumbering = new AutoNumberingEngine(Service, Log, autoNumberConfig, target, target,
				Context.OrganizationId.ToString(), inputParams);
			var result = autoNumbering.GenerateAndUpdateRecord(isUpdate: Context.MessageName == "Update");

			codeActivity.Index.Set(ExecutionContext, result.Index);
			codeActivity.IndexString.Set(ExecutionContext, result.IndexString);
			codeActivity.GeneratedString.Set(ExecutionContext, result.GeneratedString);
		}
	}
}
