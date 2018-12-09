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
	///     Version: 2.1.1
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
			isInlineConfig = autoNumberConfig.Id == Guid.Empty;
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

			var updatedAutoNumbering =
				new AutoNumbering
				{
					Id = autoNumberConfigId
				};

			// generate a string, and make sure it's unique
			var field = autoNumberConfig.FieldLogicalName?.Trim();
			var result = GenerateUniqueString(isUpdate, updatedAutoNumbering, field);
			log.Log($"Final auto-number: {result}");

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

			if (!isInlineConfig && !isBackLogged)
			{
				log.Log($"Updating auto-numbering with index {updatedAutoNumbering.CurrentIndex} ...");
				// set the new dates and index in the auto-numbering record
				service.Update(updatedAutoNumbering);
			}

			return result;
		}

		private Result GenerateUniqueString(bool isUpdate, AutoNumbering updatedAutoNumbering, string field = null)
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

				result = GenerateString(isUpdate, updatedAutoNumbering);
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

		private Result GenerateString(bool isUpdate, AutoNumbering updatedAutoNumbering)
		{
			var parser = new Parser(service, image, log);

			log.Log("Preparing attribute variables ...");
			var generatedString = parser.ParseAttributeVariables(autoNumberConfig.FormatString ?? "",
				autoNumberConfig.Owner.Id, orgId);
			log.Log($"Generated string: {generatedString}");

			if (!isInlineConfig)
			{
				if (Regex.Matches(generatedString, @"{>param\d+?}").Count > 0)
				{
					log.Log("Preparing param variables ...");
					generatedString = parser.ParseParamVariables(generatedString, inputParams);
			log.Log($"Generated string: {generatedString}");
				}
			}

			if (!isInlineConfig)
			{
				log.Log("Preparing random string variables ...");

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
			log.Log($"Generated string: {generatedString}");

			log.Log("Preparing date variables ...");
			generatedString = parser.ParseDateVariables(generatedString, autoNumberConfig.Owner.Id);
			log.Log($"Generated string: {generatedString}");

			if (!isInlineConfig)
			{
				log.Log("Preparing index ...");
				log.Log("Preparing padding ...");

				if (autoNumberConfig.IndexPadding == null)
				{
					throw new InvalidPluginExecutionException("Couldn't find the padding in the auto-numbering configuration.");
				}

				var padding = autoNumberConfig.IndexPadding >= 0 ? autoNumberConfig.IndexPadding : 0;

				log.Log("Preparing autonumber variable ...");
				generatedString = ProcessIndices(generatedString, padding.Value, isUpdate, updatedAutoNumbering);
				log.Log($"Generated string: {generatedString}");
			}

			if (!isInlineConfig && !string.IsNullOrEmpty(autoNumberConfig.ReplacementCharacters))
			{
				log.Log("Replacing characters ...");
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

				log.Log($"Generated string: {generatedString}");
			}

			return
				new Result
				{
					Index = updatedAutoNumbering.CurrentIndex.GetValueOrDefault(),
					IndexString = updatedAutoNumbering.CurrentIndex.GetValueOrDefault().ToString(),
					GeneratedString = generatedString
				};
		}

		private string ProcessIndices(string generatedString, int padding, bool isUpdate, AutoNumbering updatedAutoNumbering)
		{
			#region Date stuff

			var resetInterval = autoNumberConfig.ResetInterval;
			var resetDate = autoNumberConfig.ResetDate;
			var lastResetDate = autoNumberConfig.LastResetDate;
			var isReset = false;
			var resetValue = 0;

			// if index reset config is set, and the time has passed, then reset index to value set
			if (resetDate != null
				&& (resetInterval != AutoNumbering.ResetIntervalEnum.Never
					&& DateTime.UtcNow >= resetDate.Value
					&& (lastResetDate == null || lastResetDate < resetDate)))
			{
				lastResetDate = resetDate;

				// add the interval to the reset date
				switch (resetInterval)
				{
					case AutoNumbering.ResetIntervalEnum.Yearly:
						resetDate = resetDate.Value.AddYears(1);
						break;
					case AutoNumbering.ResetIntervalEnum.Monthly:
						resetDate = resetDate.Value.AddMonths(1);
						break;
					case AutoNumbering.ResetIntervalEnum.Daily:
						resetDate = resetDate.Value.AddDays(1);
						break;
					case AutoNumbering.ResetIntervalEnum.Once:
					case AutoNumbering.ResetIntervalEnum.Never:
						break;
					default:
						throw new InvalidPluginExecutionException("Interval does not exist in code. Please contact the administrator.");
				}

				isReset = true;
				resetValue = autoNumberConfig.ResetIndex ?? 0;
			}

			if (resetInterval == AutoNumbering.ResetIntervalEnum.Never)
			{
				resetDate = null;
			}

			#endregion

			generatedString = Regex.Replace(generatedString, @"{>index(-(.+?)::(.*?))?}",
				match =>
				{
					var index = ProcessIndex(match, isReset, resetValue, isUpdate, updatedAutoNumbering);
					return index.ToString().PadLeft(padding, '0');
				});

			updatedAutoNumbering.ResetDate = resetDate;
			updatedAutoNumbering.LastResetDate = lastResetDate;

			return generatedString;
		}

		private int ProcessIndex(Match match, bool isReset, int resetValue, bool isUpdate, AutoNumbering updatedAutoNumbering)
		{
			var isDefaultIndex = match.Value == @"{>index}";
			var currentIndex = 0;
			AutoNumberingStream stream = null;

			if (isDefaultIndex)
			{
				log.Log("Using default index.");
				currentIndex = autoNumberConfig.CurrentIndex.GetValueOrDefault();
			}
			else
			{
				if (match.Success)
				{
					log.Log($"Parsing stream '{match.Groups[1].Value}' ...");
					var fieldName = match.Groups[2].Value;
					var fieldValue = match.Groups[3].Value.IsNotEmpty()
						? match.Groups[3].Value
						: null;
					log.Log($"Field name: {fieldName}");
					log.Log($"Field value: {fieldValue}");

					log.Log($"Retrieving stream ...");
					stream =
						(from s in new XrmServiceContext(service) { MergeOption = MergeOption.NoTracking }.AutoNumberingStreamSet
						 where s.FieldName == fieldName && s.FieldValue == fieldValue
							 && s.AutoNumberingConfig == autoNumberConfig.Id
						 select new AutoNumberingStream
								{
									Id = s.Id,
									CurrentIndex = s.CurrentIndex
								}).FirstOrDefault();

					if (stream == null)
					{
						log.Log($"Couldn't find stream.");
						stream =
							new AutoNumberingStream
							{
								CurrentIndex = 0,
								FieldName = fieldName,
								FieldValue = fieldValue,
								AutoNumberingConfig = autoNumberConfig.Id
							};

						log.Log("Creating new stream ...");
						stream.Id = service.Create(stream);
						log.Log("Finished creating new stream.");
					}

					currentIndex = stream.CurrentIndex.GetValueOrDefault();
				}
			}

			log.Log($"Current index: {currentIndex}.");

			// if invalid value, reset
			// if updating and not incrementing, then keep index, else increment index
			var index = currentIndex <= 0
				? 1
				: (isUpdate && autoNumberConfig.IncrementOnUpdate != true
					? currentIndex
					: currentIndex + 1);

			if (isReset)
			{
				index = resetValue;
			}

			log.Log($"New index: {index}.");

			if (isDefaultIndex)
			{
				updatedAutoNumbering.CurrentIndex = index;
			}
			else if (stream != null)
			{
				log.Log($"Updating stream with new index ...");
				service.Update(
					new AutoNumberingStream
					{
						Id = stream.Id,
						CurrentIndex = index
					});
				log.Log($"Finished updating stream with new index.");
			}

			return index;
		}
	}
}
