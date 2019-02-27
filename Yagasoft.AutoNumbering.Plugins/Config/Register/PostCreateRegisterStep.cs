#region Imports

using System;
using System.Linq;
using Yagasoft.AutoNumbering.Plugins.Helpers;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Config.Register
{
	public class PostCreateRegisterStep : IPlugin
	{
		public void Execute(IServiceProvider serviceProvider)
		{
			new PostCreateRegisterStepLogic().Execute(this, serviceProvider);
		}
	}

	[Log]
	internal class PostCreateRegisterStepLogic : PluginLogic<PostCreateRegisterStep>
	{
		public PostCreateRegisterStepLogic() : base("Create", PluginStage.PostOperation, AutoNumbering.EntityLogicalName)
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
