#region Imports

using System;
using System.Linq;
using Yagasoft.AutoNumbering.Plugins.Helpers;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Config.Register
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
		public PreDeleteRegisterStepLogic() : base("Delete", PluginStage.PreOperation, YSAutoNumbering.EntityLogicalName)
		{
		}

		[NoLog]
		protected override void ExecuteLogic()
		{
			var preImage = Context.PreEntityImages.FirstOrDefault().Value?.ToEntity<YSAutoNumbering>();
			var postImage = Context.PostEntityImages.FirstOrDefault().Value?.ToEntity<YSAutoNumbering>();
			new RegistrationHelper(Service, Log).RegisterStageConfigSteps(preImage, postImage);
		}
	}
}
