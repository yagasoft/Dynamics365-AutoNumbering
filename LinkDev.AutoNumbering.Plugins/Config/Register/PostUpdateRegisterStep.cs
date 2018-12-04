#region Imports

using System;
using System.Linq;
using LinkDev.AutoNumbering.Plugins.Helpers;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;

#endregion

namespace LinkDev.AutoNumbering.Plugins.Config.Register
{
	public class PostUpdateRegisterStep : IPlugin
	{
		public void Execute(IServiceProvider serviceProvider)
		{
			new PostUpdateRegisterStepLogic().Execute(this, serviceProvider);
		}
	}

	[Log]
	internal class PostUpdateRegisterStepLogic : PluginLogic<PostUpdateRegisterStep>
	{
		public PostUpdateRegisterStepLogic() : base("Update", PluginStage.PostOperation, AutoNumbering.EntityLogicalName)
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
