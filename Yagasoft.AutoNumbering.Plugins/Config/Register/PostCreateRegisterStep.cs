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
		public PostCreateRegisterStepLogic() : base("Create", PluginStage.PostOperation, YSAutoNumbering.EntityLogicalName)
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
