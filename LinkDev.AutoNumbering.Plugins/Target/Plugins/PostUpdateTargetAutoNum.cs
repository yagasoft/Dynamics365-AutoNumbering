#region Imports

using System;
using System.Linq;
using LinkDev.AutoNumbering.Plugins.BLL;
using LinkDev.AutoNumbering.Plugins.Helpers;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

#endregion

namespace LinkDev.AutoNumbering.Plugins.Target.Plugins
{
	/// <summary>
	///     This plugin generates an auto-number in using the config record in the unsecure config.<br />
	///     Author: Ahmed el-Sawalhy<br />
	///     Version: 3.2.1
	/// </summary>
	public class PostUpdateTargetAutoNum : IPlugin
	{
		private readonly string config;

		public PostUpdateTargetAutoNum(string unsecureConfig, string secureConfig)
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
			new PostUpdateTargetLogic(config).Execute(this, serviceProvider);
		}
	}

	internal class PostUpdateTargetLogic : PluginLogic<PostUpdateTargetAutoNum>
	{
		private readonly string config;
		private XrmServiceContext xrmContext;

		public PostUpdateTargetLogic(string unsecureConfig) : base("Update", PluginStage.PostOperation)
		{
			config = unsecureConfig;
		}

		protected override void ExecuteLogic()
		{
			xrmContext = new XrmServiceContext(service) { MergeOption = MergeOption.NoTracking };

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

			if (!context.PostEntityImages.Any())
			{
				throw new InvalidPluginExecutionException("Couldn't find post-image for record.");
			}

			var image = context.PostEntityImages.First().Value;

			var autoNumbering = new AutoNumberingEngine(service, log, autoNumberConfig, target, image,
				context.OrganizationId.ToString());
			autoNumbering.GenerateAndUpdateRecord(true, true, isBackLogged);
		}
	}
}
