#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Yagasoft.AutoNumbering.Plugins.Helpers;
using Yagasoft.Libraries.Common;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.BLL
{
	/// <summary>
	///     Author: Ahmed Elsawalhy<br />
	///     Version: 3.1.1
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
		internal readonly ILogger Log;

		internal readonly bool IsInlineConfig;
		private readonly Entity target;
		private readonly Entity image;
		private readonly AutoNumbering autoNumberConfig;
		private readonly IEnumerable<string> inputParams;

		private int padding;

		private readonly Guid orgId;
		private readonly bool isUpdate;
		private AutoNumbering updatedAutoNumbering;

		private readonly IDictionary<string, string> cachedValues = new Dictionary<string, string>();

		internal AutoNumberingEngine(IOrganizationService service, ILogger log,
			AutoNumbering autoNumberConfig, Entity target, Entity image, Guid orgId,
			bool isUpdate = false, IEnumerable<string> inputParams = null)
		{
			Log = log;
			this.service = service;
			this.orgId = orgId;
			this.isUpdate = isUpdate;
			this.autoNumberConfig = autoNumberConfig;
			IsInlineConfig = autoNumberConfig.Id == Guid.Empty;
			this.target = target;
			this.image = image;
			this.inputParams = inputParams;
		}

		internal Result GenerateAndUpdateRecord(bool useService = true, bool isBackLogged = false)
		{
			var autoNumberConfigId = autoNumberConfig.Id;

			if (autoNumberConfig.FormatString == null)
			{
				throw new InvalidPluginExecutionException("Couldn't find a format string in the auto-numbering configuration.");
			}

			updatedAutoNumbering =
				new AutoNumbering
				{
					Id = autoNumberConfigId
				};

			// generate a string, and make sure it's unique
			var field = autoNumberConfig.FieldLogicalName?.Trim();
			var result = GenerateUniqueString(field);
			Log.Log($"Final auto-number: {result}");

			// if target and field exist, then user wants to update the record
			if (target != null && field != null)
			{
				Log.Log($"Adding generated number: '{result.GeneratedString}', to field: '{field}' ...");

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

			if (!IsInlineConfig && !isBackLogged)
			{
				Log.Log($"Updating auto-numbering with index {updatedAutoNumbering.CurrentIndex} ...");
				// set the new dates and index in the auto-numbering record
				service.Update(updatedAutoNumbering);
			}

			return result;
		}

		private Result GenerateUniqueString(string field = null)
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

				result = GenerateString();
				generatedString = result.GeneratedString;
				Log.Log($"Generated string: {generatedString}", LogLevel.Debug);

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

			Log.Log($"Accepted generated string: {generatedString}");

			return result;
		}

		private Result GenerateString()
		{
			if (!IsInlineConfig)
			{
				Log.Log("Preparing index ...");
				Log.Log("Preparing padding ...");

				if (autoNumberConfig.IndexPadding == null)
				{
					throw new InvalidPluginExecutionException("Couldn't find the padding in the auto-numbering configuration.");
				}

				padding = autoNumberConfig.IndexPadding >= 0 ? autoNumberConfig.IndexPadding.Value : 0;
			}

			Log.Log("Preparing attribute variables ...");
			var generatedString = ParseAttributeVariables(autoNumberConfig.FormatString ?? "");
			Log.Log($"Generated string: {generatedString}");

			if (!IsInlineConfig && !string.IsNullOrEmpty(autoNumberConfig.ReplacementCharacters))
			{
				Log.Log("Replacing characters ...");
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

				Log.Log($"Generated string: {generatedString}");
			}

			return
				new Result
				{
					Index = updatedAutoNumbering.CurrentIndex.GetValueOrDefault(),
					IndexString = updatedAutoNumbering.CurrentIndex.GetValueOrDefault().ToString(),
					GeneratedString = generatedString
				};
		}

		internal string ProcessIndices(string value)
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

			value = Regex.Replace(value, @"^(?:([^:]*?)(?::([^:]*?)))$",
				match =>
				{
					var index = ProcessIndex(match, isReset, resetValue);
					return index.ToString().PadLeft(padding, '0');
				});

			updatedAutoNumbering.ResetDate = resetDate;
			updatedAutoNumbering.LastResetDate = lastResetDate;

			return value;
		}

		private int ProcessIndex(Match match, bool isReset, int resetValue)
		{
			var fieldName = match.Groups[1].Value;
			var fieldValue = match.Groups[2].Value;
			fieldValue = fieldValue.IsEmpty() ? null : fieldValue;
			var isDefaultIndex = fieldName.IsEmpty();
			var currentIndex = 0;
			AutoNumberingStream stream = null;

			if (isDefaultIndex)
			{
				Log.Log("Using default index.");
				currentIndex = autoNumberConfig.CurrentIndex.GetValueOrDefault();
			}
			else if (match.Success)
			{
				Log.Log($"Parsing stream '{match.Value}' ...");
				Log.Log($"Field name: {fieldName}");
				Log.Log($"Field value: {fieldValue}");

				Log.Log($"Retrieving stream ...");
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
					Log.Log($"Couldn't find stream.");
					stream =
						new AutoNumberingStream
						{
							CurrentIndex = 0,
							FieldName = fieldName,
							FieldValue = fieldValue,
							AutoNumberingConfig = autoNumberConfig.Id
						};

					Log.Log("Creating new stream ...");
					stream.Id = service.Create(stream);
					Log.Log("Finished creating new stream.");
				}

				currentIndex = stream.CurrentIndex.GetValueOrDefault();
			}

			Log.Log($"Current index: {currentIndex}.");

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

			Log.Log($"New index: {index}.");

			if (isDefaultIndex)
			{
				updatedAutoNumbering.CurrentIndex = index;
			}
			else if (stream != null)
			{
				Log.Log($"Updating stream with new index ...");
				service.Update(
					new AutoNumberingStream
					{
						Id = stream.Id,
						CurrentIndex = index
					});
				Log.Log($"Finished updating stream with new index.");
			}
			
			return index;
		}

		internal string ParseParamVariables(int paramIndex)
		{
			var inputParamsArray = inputParams.ToArray();
			inputParamsArray.Require(nameof(inputParamsArray));

			if (paramIndex < 1 || paramIndex > inputParamsArray.Length)
			{
				throw new InvalidPluginExecutionException($"Parameter number is invalid => \"{paramIndex}\"");
			}

			return inputParamsArray[paramIndex - 1];
		}

		private string ParseAttributeVariables(string rawString)
		{
			return CrmParser.Interpreter.Parse(rawString, [typeof(Constructs)]).Evaluate(service, image, this);
		}

		private string ParseDateVariables(string rawString)
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
					rawVariable = CrmParser.Interpreter.Parse(rawVariable, [typeof(Constructs)]).Evaluate(service, image, this);

					var date = DateTime.UtcNow.ConvertToCrmUserTimeZone(service, autoNumberConfig.Owner.Id);
					return string.Format("{0:" + rawVariable + "}", date);
				});
		}
	}
}
