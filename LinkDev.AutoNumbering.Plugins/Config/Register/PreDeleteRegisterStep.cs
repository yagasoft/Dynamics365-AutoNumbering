#region Imports

using System;
using System.Linq;
using LinkDev.AutoNumbering.Plugins.Helpers;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;

#endregion

namespace LinkDev.AutoNumbering.Plugins.Config.Register
{
	public class PreDeleteRegisterStep : IPlugin
	{
		public void Execute(IServiceProvider serviceProvider)
		{
			new PreDeleteRegisterStepLogic().Execute(this, serviceProvider);
		}
	}

	[Log]
	internal class PreDeleteRegisterStepLogic : PluginLogic<PreDeleteRegisterStep>
	{
		public PreDeleteRegisterStepLogic() : base("Delete", PluginStage.PreOperation, AutoNumbering.EntityLogicalName)
		{
		}

		[NoLog]
		protected override void ExecuteLogic()
		{
			var preImage = context.PreEntityImages.FirstOrDefault().Value?.ToEntity<AutoNumbering>();
			var postImage = context.PostEntityImages.FirstOrDefault().Value?.ToEntity<AutoNumbering>();
			new RegistrationHelper(service, log).RegisterStageConfigSteps(preImage, postImage);
		}
	}
}
