#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

#endregion

namespace LinkDev.AutoNumbering.Plugins.AutoNumber.Helpers
{
	/// <summary>
	///     Author: Ahmed el-Sawalhy<br />
	///     Version: 1.3.1
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
			return rawString.Replace("{index}", indexString);
		}

		internal string ParseParamVariables(string rawString, IEnumerable<string> inputParamsParam)
		{
			var inputParams = inputParamsParam?.ToArray();
			var formatParamCount = Regex.Matches(rawString, @"{param\d+?}").Count;

			if (inputParams == null || formatParamCount > inputParams.Length)
			{
				throw new InvalidPluginExecutionException(
					$"Param count mismatch: format string param count => {formatParamCount}, "
				                                          + $"input param count => {inputParams?.Length ?? 0}");
			}

			return Regex.Replace(
				rawString, @"{param\d+?}",
				match =>
				{
					if (match.Success)
					{
						var rawVariable = match.Value;

						try
						{
							var paramIndex = int.Parse(rawVariable.Replace("{", "").Replace("}", "").Replace("param", ""));

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

		internal string ParseAttributeVariables(string rawString, Guid userIdForTimeZone, string orgId,
			bool isSuppressErrors = false)
		{

			var stringParsedCond = Regex.Replace(
				rawString, @"{\?.*?\:\:.*?}",
				match =>
				{
					if (match.Success)
					{
						var condition = match.Value.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);

						if (condition.Length <= 1)
						{
							return "";
						}

						var filledVal = condition[0].Replace("{?", "");
						// remove the ending '}'
						var emptyVal = condition[1].Substring(0, condition[1].Length - 1);

						filledVal = Regex.Replace(
							filledVal, @"{\$.*?}",
							match2 =>
							{
								if (match2.Success)
								{
									return $"{{{{{GetFieldValue(match2.Value, entity, userIdForTimeZone, orgId, true)}}}}}";
								}

								return "{{}}";
							});

						return filledVal.Contains("{{}}")
								   ? emptyVal
								   : filledVal.Replace("{{", "").Replace("}}", "");
					}

					return "";
				});

			return Regex.Replace(
				stringParsedCond, @"{\$.*?}",
				match =>
				{
					if (match.Success)
					{
						return GetFieldValue(match.Value, entity, userIdForTimeZone, orgId, isSuppressErrors);
					}

					return "";
				});
		}

		private string GetFieldValue(string rawVariable, Entity entity, Guid userIdForTimeZone, string orgId,
			bool isSuppressErrors = false)
		{
			// clean the variable from its delimiters
			var variable = rawVariable.Replace("{", "").Replace("}", "").TrimStart('$');
			var field = variable.Split('$');
			var fieldNameRaw = field[0];
			var fieldName = field[0].Split('@')[0];

			if (!entity.Contains(fieldName))
			{
				if (isSuppressErrors)
				{
					return null;
				}

				throw new InvalidPluginExecutionException($"Couldn't parse the format string -- missing value \"{fieldName}\".");
			}

			// get the attribute
			var fieldValue = entity.GetAttributeValue<object>(fieldName);
			// will be used if there is recursive lookup
			var column = fieldName;

			if (fieldValue == null)
			{
				return null;
			}

			// variable is recursive, so we need to go deeper through the lookup
			if (variable.Contains("$"))
			{
				// if the field value is not a lookup, then we can't recurse
				var reference = fieldValue as EntityReference;

				if (reference == null)
				{
					throw new InvalidPluginExecutionException($"Field \"{fieldName}\" is not a lookup.");
				}

				// we don't need the first value anymore, as it references the lookup itself
				column = variable.Split('$')[1];

				// get lookup entity
				entity = service.Retrieve(reference.LogicalName, reference.Id, new ColumnSet(column));

				if (!entity.Contains(column))
				{
					if (isSuppressErrors)
					{
						return null;
					}

					throw new InvalidPluginExecutionException($"Couldn't parse the format string -- missing value " +
					                                          $"\"{column}\" in entity \"{entity.LogicalName}\".");
				}

				fieldValue = entity[column];

				// it goes deeper!
				if (variable.Split('$').Length > 2)
				{
					return GetFieldValue(variable.Replace(fieldName, ""), entity, userIdForTimeZone, orgId, isSuppressErrors);
				}
			}

			#region Attribute processors

			if (fieldValue is string)
			{
				return (string) fieldValue;
			}

			if (fieldValue is OptionSetValue)
			{
				var label = entity.FormattedValues.FirstOrDefault(keyVal => keyVal.Key == column).Value
				            ?? Helper.GetOptionsSetTextForValue(service, entity.LogicalName, column,
					            (fieldValue as OptionSetValue).Value, orgId);
				return label ?? "";
			}
			
			if (fieldValue is DateTime)
			{
				var dateFormatRaw = fieldNameRaw.Split('@');
				var date = ((DateTime) fieldValue).ConvertToCrmUserTimeZone(service, userIdForTimeZone);
				return dateFormatRaw.Length > 1 ? string.Format("{0:" + dateFormatRaw[1] + "}", date) : date.ToString();
			}

			var fieldRef = fieldValue as EntityReference;

			if (fieldRef != null)
			{
				return fieldRef.Name ?? Helper.GetEntityPrimaryFieldValue(service, fieldRef, orgId);
			}

			#endregion

			return fieldValue?.ToString();
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
