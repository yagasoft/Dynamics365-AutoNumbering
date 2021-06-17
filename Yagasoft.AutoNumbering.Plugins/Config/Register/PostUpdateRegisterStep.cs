﻿#region Imports

using System;
using System.Linq;
using Yagasoft.AutoNumbering.Plugins.Helpers;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Config.Register
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
			var preImage = Context.PreEntityImages.FirstOrDefault().Value?.ToEntity<AutoNumbering>();
			var postImage = Context.PostEntityImages.FirstOrDefault().Value?.ToEntity<AutoNumbering>();
			new RegistrationHelper(Service, Log).RegisterStageConfigSteps(preImage, postImage);
		}
	}
}
