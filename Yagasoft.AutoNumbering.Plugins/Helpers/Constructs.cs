#region Imports

using Yagasoft.AutoNumbering.Plugins.BLL;
using static Yagasoft.Libraries.Common.CrmParser;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Helpers
{
	public class Constructs
	{
		[Expression]
		public class IndexExpression(ParserContext context) : FunctionExpression(context)
		{
			protected override string FinalForm => "^[$]sequence$";
			protected override string RecognisePattern => "^[$](?:s(?:e(?:q(?:u(?:e(?:n(?:c(?:e)?)?)?)?)?)?)?)?$";

			protected override object FunctionEvaluate(object baseValue = null)
			{
				if (globalState.ContextObject is not AutoNumberingEngine contextObject
					|| contextObject.IsInlineConfig)
				{
					return null;
				}

				Parameters = EvaluateParameters(baseValue);
				var keyName = GetParam<string>("Key", 0);
				var value = GetParam<object>("Value", 1);

				return contextObject.ProcessIndices($"{keyName}:{value}");
			}
		}

		[Expression]
		public class ParamExpression(ParserContext context) : FunctionExpression(context)
		{
			protected override string FinalForm => "^[$]inparam";
			protected override string RecognisePattern => "^[$](?:i(?:n(?:p(?:a(?:r(?:a(?:m)?)?)?)?)?)?)?$";

			protected override object FunctionEvaluate(object baseValue = null)
			{
				if (globalState.ContextObject is not AutoNumberingEngine contextObject || contextObject.IsInlineConfig)
				{
					return null;
				}

				Parameters = EvaluateParameters(baseValue);
				
				contextObject.Log.Log("Preparing param variables ...");
				var buffer = contextObject.ParseParamVariables(GetParam<int?>("Index", 0, true) ?? 0);
				contextObject.Log.Log($"Generated string: {buffer}");

				return buffer;
			}
		}
	}
}
