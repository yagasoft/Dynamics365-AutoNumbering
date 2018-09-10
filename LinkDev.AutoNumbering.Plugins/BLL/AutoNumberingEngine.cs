#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LinkDev.AutoNumbering.Plugins.Helpers;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

#endregion

namespace LinkDev.AutoNumbering.Plugins.BLL
{
	/// <summary>
	///     Author: Ahmed el-Sawalhy<br />
	///     Version: 1.3.1
	/// </summary>
	[Log]
	internal class AutoNumberingEngine
	{
		internal class Result
		{
			internal int Index;
			internal string IndexString = "";
			internal string GeneratedString = "";
		}

		private readonly IOrganizationService service;
		private readonly CrmLog log;

		private readonly bool isInlineConfig;
		private readonly Entity target;
		private readonly Entity image;
		private readonly AutoNumbering autoNumberConfig;
		private readonly IEnumerable<string> inputParams;

		private readonly string orgId;

		internal AutoNumberingEngine(IOrganizationService service, CrmLog log,
			AutoNumbering autoNumberConfig, Entity target, Entity image, string orgId,
			IEnumerable<string> inputParams = null)
		{
			this.log = log;
			this.service = service;
			this.orgId = orgId;
			this.autoNumberConfig = autoNumberConfig;
			isInlineConfig = this.autoNumberConfig.Id == Guid.Empty;
			this.target = target;
			this.image = image;
			this.inputParams = inputParams;
		}

		internal Result GenerateAndUpdateRecord(bool useService = true, bool isUpdate = false, bool isBackLogged = false)
		{
			var autoNumberConfigId = autoNumberConfig.Id;

			if (autoNumberConfig.FormatString == null)
			{
				throw new InvalidPluginExecutionException("Couldn't find a format string in the auto-numbering configuration.");
			}

			var isUseIndex = !isInlineConfig && !isBackLogged && autoNumberConfig.FormatString.Contains("{index}");

			if (isUseIndex && autoNumberConfig.CurrentIndex == null)
			{
				throw new InvalidPluginExecutionException("Couldn't find an index in the auto-numbering configuration.");
			}

			var updatedAutoNumbering =
				new AutoNumbering
				{
					Id = autoNumberConfigId
				};

			var index = autoNumberConfig.CurrentIndex ?? -1;

			if (isUseIndex)
			{
				index = Helper.ProcessIndex(autoNumberConfig, isUpdate, updatedAutoNumbering);
			}

			// generate a string, and make sure it's unique
			var field = autoNumberConfig.FieldLogicalName?.Trim();

			var result = GenerateUniqueString(index, field);

			// if target and field exist, then user wants to update the record
			if (target != null && field != null)
			{
				log.Log($"Adding generated number: '{result.GeneratedString}', to field: '{field}' ...");

				if (useService)
				{
					var updatedTarget =
						new Entity(target.LogicalName)
						{
							Id = target.Id,
							[field] = result.GeneratedString
						};
					service.Update(updatedTarget);
				}
				else
				{
					target[field] = result.GeneratedString;
				}
			}

			if (isUseIndex)
			{
				log.Log($"Updating auto-numbering with index {index} ...");
				// set the new dates and index in the auto-numbering record
				service.Update(updatedAutoNumbering);
			}

			return result;
		}

		private Result GenerateUniqueString(int index, string field = null)
		{
			var generatedString = "";
			bool isUnique;
			var iterations = 1;
			Result result;

			if (autoNumberConfig.ValidateUniqueString != false)
			{
				if (field == null)
				{
					throw new InvalidPluginExecutionException(
						"To generate a unique string, a target field must be specified in the config record.");
				}

				if (target == null)
				{
					throw new InvalidPluginExecutionException(
						"To generate a unique string, a target must be passed as a parameter to the execution.");
				}
			}

			do
			{
				if (iterations > 10)
				{
					throw new InvalidPluginExecutionException($"Couldn't generate a unique random string => \"{generatedString}\"");
				}

				result = GenerateString(index);
				generatedString = result.GeneratedString;
				log.Log($"Generated string: {generatedString}", LogLevel.Debug);

				isUnique = autoNumberConfig.ValidateUniqueString != false
					&& field != null && target != null
					&& new OrganizationServiceContext(service)
						.CreateQuery(target.LogicalName)
						.Where(targetQ => (string)targetQ[field] == generatedString)
						.Select(targetQ => targetQ[field])
						.ToList().Count <= 0;

				iterations++;
			}
			while (autoNumberConfig.ValidateUniqueString != false && !isUnique);

			log.Log($"Accepted generated string: {generatedString}");

			return result;
		}

		private Result GenerateString(int index)
		{
			var parser = new Parser(service, image, log);

			log.Log("Preparing attribute variables ...");
			var generatedString = parser.ParseAttributeVariables(autoNumberConfig.FormatString ?? "",
				autoNumberConfig.Owner.Id, orgId, isInlineConfig);

			if (!isInlineConfig)
			{
				if (Regex.Matches(generatedString, @"{param\d+?}").Count > 0)
				{
					log.Log("Preparing param variables ...");
					generatedString = parser.ParseParamVariables(generatedString, inputParams);
				}
			}

			log.Log("Preparing random string variables ...");

			if (!isInlineConfig)
			{
				if (autoNumberConfig.IsRandomLetterStart == null || autoNumberConfig.IsNumberLetterRatio == null)
				{
					throw new InvalidPluginExecutionException(
						"Couldn't find a setting for the ratio or letter start in the auto-numbering configuration.");
				}
			}

			generatedString = parser.ParseRandomStringVariables(generatedString, autoNumberConfig.IsRandomLetterStart == true,
				(autoNumberConfig.IsNumberLetterRatio == true && autoNumberConfig.NumberLetterRatio != null)
					? autoNumberConfig.NumberLetterRatio.Value
					: -1);

			log.Log("Preparing date variables ...");
			generatedString = parser.ParseDateVariables(generatedString, autoNumberConfig.Owner.Id);

			log.Log("Preparing index ...");
			var indexString = index.ToString();

			if (!isInlineConfig)
			{
				log.Log("Preparing padding ...");

				if (autoNumberConfig.IndexPadding == null)
				{
					throw new InvalidPluginExecutionException("Couldn't find the padding in the auto-numbering configuration.");
				}

				var padding = autoNumberConfig.IndexPadding >= 0 ? autoNumberConfig.IndexPadding : 0;

				log.Log("Preparing autonumber variable ...");
				generatedString = parser.ParseAutoNumberVariable(generatedString, indexString.PadLeft(padding.Value, '0'));
			}

			if (!isInlineConfig && !string.IsNullOrEmpty(autoNumberConfig.ReplacementCharacters))
			{
				var pairs = autoNumberConfig.ReplacementCharacters.Split(';').Select(e => e.Split(',')).ToArray();

				if (pairs.Any(e => e.Length < 2))
				{
					throw new InvalidPluginExecutionException(
						$"Replacement Characters' config is invalid: {autoNumberConfig.ReplacementCharacters}.");
				}

				foreach (var pair in pairs)
				{
					generatedString = Regex.Replace(generatedString, pair[0], pair[1]);
				}
			}

			return
				new Result
				{
					Index = index,
					IndexString = indexString,
					GeneratedString = generatedString
				};
		}
	}
}
