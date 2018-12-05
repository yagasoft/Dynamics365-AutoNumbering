#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using static LinkDev.Libraries.Common.CrmHelpers;

#endregion

namespace LinkDev.AutoNumbering.Plugins.Helpers
{
	/// <summary>
	///     Author: Ahmed el-Sawalhy<br />
	///     Version: 2.1.1
	/// </summary>
	[Log]
	internal class Parser
	{
		private readonly IOrganizationService service;
		private readonly Entity entity;
		private readonly CrmLog log;

		internal Parser(IOrganizationService service, Entity entity, CrmLog log)
		{
			this.log = log;
			this.service = service;
			this.entity = entity;
		}

		internal string ParseAutoNumberVariable(string rawString, string indexString)
		{
			return Regex.Replace(rawString, @"{>index\d*?}", indexString);
		}

		internal string ParseParamVariables(string rawString, IEnumerable<string> inputParamsParam)
		{
			var inputParams = inputParamsParam?.ToArray();
			var formatParamCount = Regex.Matches(rawString, @"{>param\d+?}").Count;

			if (inputParams == null || formatParamCount > inputParams.Length)
			{
				throw new InvalidPluginExecutionException(
					$"Param count mismatch: format string param count => {formatParamCount}, "
						+ $"input param count => {inputParams?.Length ?? 0}");
			}

			return Regex.Replace(
				rawString, @"{>param(\d+?)}",
				match =>
				{
					if (match.Success)
					{
						var rawVariable = match.Groups[1].Value;

						try
						{
							var paramIndex = int.Parse(rawVariable);

							if (paramIndex < 1 || paramIndex > inputParams.Length)
							{
								throw new InvalidPluginExecutionException($"Parameter number is invalid => \"{rawVariable}\"");
							}

							return inputParams[paramIndex - 1];
						}
						catch (FormatException)
						{
							throw new InvalidPluginExecutionException($"Parameter number is invalid => \"{rawVariable}\"");
						}
					}

					return "";
				});
		}

		internal string ParseAttributeVariables(string rawString, Guid userIdForTimeZone, string orgId)
		{
			var otherPlaceholders = new[] { ">index.*?", ">param.*?", "!.*?", "@.*?" };

			foreach (var placeholder in otherPlaceholders)
			{
				rawString = Regex.Replace(rawString, $"{{{placeholder}}}",
					match =>
					{
						if (match.Success)
						{
							return $"$$$$${match.Value.Substring(1, match.Value.Length - 2)}#####";
						}

						return "";
					});
			}

			var parsedString = Libraries.Common.CrmHelpers.ParseAttributeVariables(service, rawString, entity,
				userIdForTimeZone, orgId);

			return parsedString.Replace("$$$$$", "{").Replace("#####", "}");
		}

		internal string ParseRandomStringVariables(string rawString, bool isLetterStart, int numberLetterRatio = -1)
		{
			return Regex.Replace(
				rawString, @"{!.*?}",
				match =>
				{
					if (match.Success)
					{
						var rawVariable = match.Value;

						// clean the variables from their delimiters, and extract its sections
						var variable = rawVariable.Replace("{", "").Replace("}", "").TrimStart('!').Split('-');

						if (variable.Length < 2)
						{
							throw new InvalidPluginExecutionException("Couldn't parse the random string! => " + rawVariable);
						}

						// if a pool of symbols was already supplied
						if (variable[0].Contains(","))
						{
							return RandomGenerator.GetRandomString(int.Parse(variable[1]), isLetterStart,numberLetterRatio,
								variable[0].Split(','));
						}
						else
						{
							if (!variable[0].Contains("u") && !variable[0].Contains("l") && !variable[0].Contains("n"))
							{
								throw new InvalidPluginExecutionException("Couldn't parse the random string! => " + rawVariable);
							}

							const string flagIndexer = "uln";
							var flags = variable[0].Select(flag => (RandomGenerator.SymbolFlag) flagIndexer.IndexOf(flag))
								.ToList();

							return RandomGenerator.GetRandomString(int.Parse(variable[1]), isLetterStart,numberLetterRatio,
								flags.ToArray());
						}
					}

					return "";
				});
		}

		internal string ParseDateVariables(string rawString, Guid userIdForTimeZone)
		{
			return Regex.Replace(
				rawString, @"{@.*?}",
				match =>
				{
					if (match.Success)
					{
						var rawVariable = match.Value;

						// clean the variables from their delimiters, and format it as a date
						var variable = rawVariable.Replace("{", "").Replace("}", "").TrimStart('@');
						var date = DateTime.UtcNow.ConvertToCrmUserTimeZone(service, userIdForTimeZone);
						return string.Format("{0:" + variable + "}", date);
					}

					return "";
				});
		}
	}
}
