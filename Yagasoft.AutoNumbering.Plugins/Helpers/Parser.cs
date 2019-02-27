#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using static Yagasoft.Libraries.Common.CrmHelpers;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Helpers
{
	/// <summary>
	///     Author: Ahmed Elsawalhy<br />
	///     Version: 2.1.2
	/// </summary>
	[Log]
	internal class Parser
	{
		private readonly IOrganizationService service;
		private readonly Entity entity;
		private readonly CrmLog log;

		private readonly Guid userIdForTimeZone;
		private readonly string orgId;

		private readonly IDictionary<string, string> cachedValues;


		internal Parser(IOrganizationService service, Entity entity, CrmLog log, Guid userIdForTimeZone, string orgId, 
			IDictionary<string, string> cachedValues)
		{
			this.log = log;
			this.service = service;
			this.entity = entity;
			this.userIdForTimeZone = userIdForTimeZone;
			this.orgId = orgId;
			this.cachedValues = cachedValues;
		}

		internal string ParseParamVariables(string rawString, IEnumerable<string> inputParamsParam)
		{
			var inputParams = inputParamsParam?.ToArray();
			inputParams.Require(nameof(inputParams));

			return Regex.Replace(
				rawString, @"{!param!((?>(?<c1>{)|[^{}]+?|(?<-c1>}))*?)(?(c1)(?!))}",
				match =>
				{
					if (!match.Success)
					{
						return "";
					}

					var rawVariable = match.Groups[1].Value;

					try
					{
						rawVariable = Libraries.Common.CrmHelpers.ParseAttributeVariables(service, rawVariable, entity,
							userIdForTimeZone, orgId, cachedValues);

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
				});
		}

		internal string ParseAttributeVariables(string rawString)
		{
			return Libraries.Common.CrmHelpers.ParseAttributeVariables(service, rawString, entity,
				userIdForTimeZone, orgId, cachedValues);
		}

		internal string ParseRandomStringVariables(string rawString, bool isLetterStart, int numberLetterRatio = -1)
		{
			return Regex.Replace(
				rawString, @"{!rand!((?>(?<c1>{)|[^{}]+?|(?<-c1>}))*?)(?(c1)(?!))}",
				matchT =>
				{
					if (!matchT.Success)
					{
						return "";
					}

					var rawVariable = matchT.Groups[1].Value;
					rawVariable = Libraries.Common.CrmHelpers.ParseAttributeVariables(service, rawVariable, entity,
						userIdForTimeZone, orgId, cachedValues);

					return Regex.Replace(
						rawVariable, @"^(?:\$((?:u|l|n){1,3})|([^$](?:.*?,?)*))-(\d+)$",
						match =>
						{
							if (!match.Success)
							{
								return "";
							}

							var rawPools = match.Groups[1].Value;
							var customPool = match.Groups[2].Value;
							var length = int.Parse(match.Groups[3].Value);

							if (rawPools.IsNotEmpty())
							{
								const string flagIndexer = "uln";
								var flags = rawPools.Select(flag => (RandomGenerator.SymbolFlag)flagIndexer.IndexOf(flag))
									.ToList();

								return RandomGenerator.GetRandomString(length, isLetterStart, numberLetterRatio,
									flags.ToArray());
							}

							return RandomGenerator.GetRandomString(length, isLetterStart, numberLetterRatio,
								customPool.Split(','));
						});
				});
		}

		internal string ParseDateVariables(string rawString)
		{
			return Regex.Replace(
				rawString, @"{!now!((?>(?<c1>{)|[^{}]+?|(?<-c1>}))*?)(?(c1)(?!))}",
				match =>
				{
					if (!match.Success)
					{
						return "";
					}

					var rawVariable = match.Groups[1].Value;
					rawVariable = Libraries.Common.CrmHelpers.ParseAttributeVariables(service, rawVariable, entity,
						userIdForTimeZone, orgId, cachedValues);

					var date = DateTime.UtcNow.ConvertToCrmUserTimeZone(service, userIdForTimeZone);
					return string.Format("{0:" + rawVariable + "}", date);
				});
		}
	}
}
