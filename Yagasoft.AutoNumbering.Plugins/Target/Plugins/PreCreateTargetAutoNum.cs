#region Imports

using System;
using System.Linq;
using System.Reflection;
using Yagasoft.AutoNumbering.Plugins.BLL;
using Yagasoft.AutoNumbering.Plugins.Helpers;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Target.Plugins
{
	/// <summary>
	///     This plugin generates an auto-number in using the config record in the unsecure config.<br />
	///     Author: Ahmed Elsawalhy<br />
	///     Version: 3.2.1
	/// </summary>
	public class PreCreateTargetAutoNum : IPlugin
	{
		private readonly string config;

		public PreCreateTargetAutoNum(string unsecureConfig, string secureConfig)
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
			new PreCreateTargetLogic(config).Execute(this, serviceProvider);
		}
	}

	internal class PreCreateTargetLogic : PluginLogic<PreCreateTargetAutoNum>
	{
		private readonly string config;
		private XrmServiceContext xrmContext;

		public PreCreateTargetLogic(string unsecureConfig) : base("Create", PluginStage.PreOperation)
		{
			config = unsecureConfig;
		}

		protected override void ExecuteLogic()
		{
			xrmContext = new XrmServiceContext(Service) {MergeOption = MergeOption.NoTracking};

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

			var autoNumbering = new AutoNumberingEngine(Service, Log, autoNumberConfig, target, image, Context.OrganizationId);
			try
			{
				autoNumbering.GenerateAndUpdateRecord(false, isBackLogged);
			}
			catch (ReflectionTypeLoadException e)
			{
				throw new Exception(e.LoaderExceptions.Select(e1 => e1.BuildShortExceptionMessage()).StringAggregate("\r\n\r\n"));
			}
		}
	}
}
