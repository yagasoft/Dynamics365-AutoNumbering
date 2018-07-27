#region Imports

using System;
using System.Linq;
using System.Text.RegularExpressions;
using LinkDev.AutoNumbering.Plugins.AutoNumber.BLL;
using LinkDev.AutoNumbering.Plugins.AutoNumber.Helpers;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using static LinkDev.Libraries.Common.CrmHelpers;

#endregion

namespace LinkDev.AutoNumbering.Plugins.AutoNumber.Core
{
	/// <summary>
	/// DEPRECATED
	/// </summary>
	public class AutoNumberPlugin : IPlugin
	{
		private readonly string config;

		public AutoNumberPlugin(string unsecureConfig, string secureConfig)
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
			new AutoNumberPluginLogic(config).Execute(this, serviceProvider);
		}
	}

	[Log]
	internal class AutoNumberPluginLogic : PluginLogic<AutoNumberPlugin>
	{
		private readonly string config;
		private XrmServiceContext xrmContext;

		public AutoNumberPluginLogic(string unsecureConfig) : base(null, PluginStage.All)
		{
			config = unsecureConfig;
		}

		[NoLog]
		protected override void ExecuteLogic()
		{
			xrmContext = new XrmServiceContext(service) {MergeOption = MergeOption.NoTracking};

			log.Log("Getting target ...");
			var target = (Entity)context.InputParameters["Target"];


			var autoNumberConfig = Helper.GetAutoNumberingConfig(target, config, context, service, log, out var isBackLogged);

			if (autoNumberConfig == null)
			{
				log.Log($"Exiting.", LogLevel.Warning);
				return;
			}

			if (autoNumberConfig.FieldLogicalName == null)
			{
				throw new InvalidPluginExecutionException(
					"Target field must be specified in the config record for plugin execution.");
			}

			var image = target;

			// to avoid problems with missing fields that are needed by the parser, fetch the whole record
			if (context.MessageName == "Update")
			{
				// post-operation
				if (context.Stage == 40)
				{
					if (!context.PostEntityImages.Any())
					{
						throw new InvalidPluginExecutionException("Couldn't find post-image for record.");
					}

					image = context.PostEntityImages.First().Value;
				}
				else
				{
					// to avoid problems with missing fields that are needed by the parser, fetch the whole record
					// if the format string doesn't contain an attribute reference, then skip
					if (Regex.IsMatch(autoNumberConfig.FormatString, @"{\$.*?}"))
					{
						var columns = Regex.Matches(autoNumberConfig.FormatString, @"{\$.*?}").Cast<Match>()
							.Select(match => match.Value.Replace("{", "").Replace("}", "").TrimStart('$').Split('$')[0]).ToArray();
						image = service.Retrieve(target.LogicalName, target.Id, new ColumnSet(columns));

						// update with new values in the pipeline
						foreach (var keyValuePair in target.Attributes)
						{
							image[keyValuePair.Key] = keyValuePair.Value;
						}
					}
				}
			}

			var autoNumbering = new AutoNumberingEngine(service, log, autoNumberConfig, target, image,
				context.OrganizationId.ToString());
			autoNumbering.GenerateAndUpdateRecord(context.Stage == 40, context.MessageName == "Update", isBackLogged);
		}

		[NoLog]
		protected override bool IsContextValid()
		{
			return context.MessageName == "Create" || context.MessageName == "Update";
		}
	}
}
