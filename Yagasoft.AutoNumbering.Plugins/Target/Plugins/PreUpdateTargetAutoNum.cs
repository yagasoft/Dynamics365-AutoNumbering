#region Imports

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Yagasoft.AutoNumbering.Plugins.BLL;
using Yagasoft.AutoNumbering.Plugins.Helpers;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Target.Plugins
{
	/// <summary>
	///     This plugin generates an auto-number in using the config record in the unsecure config.<br />
	///     Author: Ahmed Elsawalhy<br />
	///     Version: 3.2.1
	/// </summary>
	public class PreUpdateTargetAutoNum : IPlugin
	{
		private readonly string config;

		public PreUpdateTargetAutoNum(string unsecureConfig, string secureConfig)
		{
			if (string.IsNullOrEmpty(unsecureConfig))
			{
				throw new InvalidPluginExecutionException(
					"Plugin config is empty. Please enter the config record ID or inline config first.");
			}

			config = unsecureConfig;
		}

		public void Execute(IServiceProvider serviceProvider)
		{
			new PreUpdateTargetLogic(config).Execute(this, serviceProvider);
		}
	}

	internal class PreUpdateTargetLogic : PluginLogic<PreUpdateTargetAutoNum>
	{
		private readonly string config;
		private XrmServiceContext xrmContext;

		public PreUpdateTargetLogic(string unsecureConfig) : base("Update", PluginStage.PreOperation)
		{
			config = unsecureConfig;
		}

		protected override void ExecuteLogic()
		{
			xrmContext = new XrmServiceContext(Service) { MergeOption = MergeOption.NoTracking };

			Log.Log("Getting target ...");
			var target = (Entity)Context.InputParameters["Target"];


			var autoNumberConfig = Helper.GetAutoNumberingConfig(target, config,
                Context as IPluginExecutionContext, Service, Log, out var isBackLogged);

			if (autoNumberConfig == null)
			{
				Log.Log($"Exiting.", LogLevel.Warning);
				return;
			}

			if (autoNumberConfig.FieldLogicalName == null)
			{
				throw new InvalidPluginExecutionException(
					"Target field must be specified in the config record for plugin execution.");
			}

			var image = target;

			// to avoid problems with missing fields that are needed by the parser, fetch the whole record
			// if the format string doesn't contain an attribute reference, then skip
			if (Regex.IsMatch(autoNumberConfig.FormatString, @"{\$.*?}"))
			{
				var columns = Regex.Matches(autoNumberConfig.FormatString, @"{\$.*?}").Cast<Match>()
					.Select(match => match.Value.Replace("{", "").Replace("}", "").TrimStart('$').Split('$')[0]).ToArray();
				image = Service.Retrieve(target.LogicalName, target.Id, new ColumnSet(columns));

				// update with new values in the pipeline
				foreach (var keyValuePair in target.Attributes)
				{
					image[keyValuePair.Key] = keyValuePair.Value;
				}
			}

			var autoNumbering = new AutoNumberingEngine(Service, Log, autoNumberConfig, target, image,
				Context.OrganizationId.ToString());
			autoNumbering.GenerateAndUpdateRecord(false, true, isBackLogged);
		}
	}
}
