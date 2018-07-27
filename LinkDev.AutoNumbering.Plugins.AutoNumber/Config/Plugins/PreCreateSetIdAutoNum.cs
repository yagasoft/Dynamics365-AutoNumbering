#region Imports

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

#endregion

namespace LinkDev.AutoNumbering.Config.Plugins
{
	/// <summary>
	///     Author: Ahmed el-Sawalhy<br />
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
			AutoNumbering.Plugins.AutoNumber.AutoNumbering.EntityLogicalName)
		{ }

		protected override void ExecuteLogic()
		{
			var target = (Entity) context.InputParameters["Target"];
			target[AutoNumbering.Plugins.AutoNumber.AutoNumbering.Fields.UniqueID] = target.Id.ToString();
		}
	}
}
