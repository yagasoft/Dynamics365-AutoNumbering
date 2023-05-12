using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Yagasoft.AutoNumbering.Plugins.BLL;
using Yagasoft.Libraries.Common;

namespace Yagasoft.AutoNumbering.Plugins.Helpers
{
    public class Constructs
    {
		[CrmParser.Construct("j", "index")]
		public class IndexConstruct : CrmParser.DefaultContextConstruct
		{
			public IndexConstruct(CrmParser.GlobalState state, string constructString, string parameters, IEnumerable<CrmParser.Preprocessor> preProcessors,
				IEnumerable<CrmParser.PostProcessor> postProcessors)
				: base(state, constructString, parameters, preProcessors, postProcessors)
			{ }

			protected override string ExecuteContextLogic(Entity context, string buffer)
			{
				var contextObject = State.ContextObject as AutoNumberingEngine;

				if (contextObject == null || contextObject.IsInlineConfig)
				{
					return null;
				}

				return contextObject.ProcessIndices(buffer);
			}
		}

		[CrmParser.Construct("m", "param")]
		public class ParamConstruct : CrmParser.DefaultContextConstruct
		{
			public ParamConstruct(CrmParser.GlobalState state, string constructString, string parameters, IEnumerable<CrmParser.Preprocessor> preProcessors,
				IEnumerable<CrmParser.PostProcessor> postProcessors)
				: base(state, constructString, parameters, preProcessors, postProcessors)
			{ }

			protected override string ExecuteContextLogic(Entity context, string buffer)
			{
				var contextObject = State.ContextObject as AutoNumberingEngine;

				if (contextObject == null || contextObject.IsInlineConfig)
				{
					return null;
				}

				contextObject.Log.Log("Preparing param variables ...");

				buffer = int.TryParse(buffer, out var index)
					? contextObject.ParseParamVariables(index)
					: throw new InvalidPluginExecutionException($"Parameter number is invalid => \"{buffer}\"");

				contextObject.Log.Log($"Generated string: {buffer}");

				return buffer;
			}
		}
    }
}
