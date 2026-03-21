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
			YSAutoNumbering.EntityLogicalName)
		{ }

		protected override void ExecuteLogic()
		{
			var target = (Entity) Context.InputParameters["Target"];
			target[YSAutoNumbering.Fields.UniqueID] = target.Id.ToString();
		}
	}
}
