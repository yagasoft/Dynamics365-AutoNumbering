#region Imports

using System;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Config.Plugins
{
	/// <summary>
	///     Author: Ahmed Elsawalhy<br />
	///     Version: 1.2.1
	/// </summary>
	public class PreCreateSetIdAutoNum : IPlugin
	{
		public void Execute(IServiceProvider serviceProvider)
		{
			new PreCreateSetIdAutoNumLogic().Execute(this, serviceProvider);
		}
	}

	internal class PreCreateSetIdAutoNumLogic : PluginLogic<PreCreateSetIdAutoNum>
	{
		public PreCreateSetIdAutoNumLogic() : base("Create", PluginStage.PreOperation,
			AutoNumbering.EntityLogicalName)
		{ }

		protected override void ExecuteLogic()
		{
			var target = (Entity) context.InputParameters["Target"];
			target[AutoNumbering.Fields.UniqueID] = target.Id.ToString();
		}
	}
}
