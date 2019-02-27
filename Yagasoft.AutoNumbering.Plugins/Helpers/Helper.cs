#region Imports

using System;
using System.Linq;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using static Yagasoft.Libraries.Common.CrmHelpers;

#endregion

namespace Yagasoft.AutoNumbering.Plugins.Helpers
{
	/// <summary>
	///     Author: Ahmed Elsawalhy<br />
	///     Version: 1.2.1
	/// </summary>
	internal static class Helper
	{
		internal static AutoNumbering PreValidation(IOrganizationService service, Entity target,
			AutoNumbering autoNumberingConfig, CrmLog log, bool isConditioned, bool isBackLogged)
		{
			if (autoNumberingConfig == null)
			{
				// condition satisfaction failed before, so there's nothing to do here
				if (isConditioned)
				{
					return null;
				}

				throw new InvalidPluginExecutionException("Couldn't find an auto-numbering configuration.");
			}

			// if the auto-numbering is inactive, then don't apply
			if (autoNumberingConfig.Status == AutoNumbering.StatusEnum.Inactive)
			{
				//throw new InvalidPluginExecutionException("Autonumbering config record is inactive.");
				log.Log("AutoNumbering record is inactive.");
				return null;
			}

			// exit if auto-number field is updating to prevent looping
			if (target != null && autoNumberingConfig.FieldLogicalName != null
				&& target.Contains(autoNumberingConfig.FieldLogicalName))
			{
				log.Log("Numbering is in progress in another instance.");
				return null;
			}

			if (autoNumberingConfig.Id == Guid.Empty)
			{
				log.Log("Using inline config.");
				return autoNumberingConfig;
			}

			if (autoNumberingConfig.Condition != null
				&& autoNumberingConfig.EntityLogicalName_ldv_EntityLogicalName == null)
			{
				throw new InvalidPluginExecutionException("Condition is set but entity name is not set in auto-numbering config.");
			}

			// only lock if an index is needed
			if (autoNumberingConfig.FormatString.Contains("{!index!}") && !isBackLogged)
			{
				log.Log("Locking ...", LogLevel.Debug);

				// ensure locking
				service.Update(
					new AutoNumbering
					{
						Id = autoNumberingConfig.Id,
						Locking = new Random().Next(999999, 999999999).ToString()
					});

				log.Log("Refetching numbering record ...", LogLevel.Debug);

				// get it again to ensure locking took effect
				var currentIndex =
					(from autoNumberQ in new XrmServiceContext(service).AutoNumberingSet
					 where autoNumberQ.AutoNumberingId == autoNumberingConfig.Id
						 && autoNumberQ.Status == AutoNumbering.StatusEnum.Active
					 select autoNumberQ.CurrentIndex).First();

				autoNumberingConfig.CurrentIndex = currentIndex;
			}

			return autoNumberingConfig;
		}

		internal static AutoNumbering GetAutoNumberingConfig(Entity target, string config,
			IPluginExecutionContext context, IOrganizationService service, CrmLog log, out bool isBackLogged)
		{
			var xrmContext = new XrmServiceContext(service) { MergeOption = MergeOption.NoTracking };

			var configIds = config.Split(',').Select(item => item.Trim()).ToArray();
			var isInlineConfig = config.Contains(";;");

			AutoNumbering autoNumberConfig = null;

			if (isInlineConfig)
			{
				autoNumberConfig = GetInlineConfig(config, context.UserId);
			}

			// get it once to check for generator field update below
			log.Log("Getting auto-numbering config ...");

			// if a condition is found in any of the configs, then don't throw an error if none match
			// it simply means that the user wants the auto-numbering to work only if the condition is satisified
			// this is useful for multiple fields
			var isConditioned = false;

			// if using a backlog, then no need to lock
			isBackLogged = false;

			if (!isInlineConfig)
			{
				var autoNumberConfigTemp = GetBackloggedConfig(context, service, log);

				if (autoNumberConfigTemp == null)
				{
					foreach (var configId in configIds)
					{
						autoNumberConfigTemp =
							(from autoNumberQ in xrmContext.AutoNumberingSet
							 where autoNumberQ.UniqueID == configId || autoNumberQ.Name == configId
								 && autoNumberQ.Status == AutoNumbering.StatusEnum.Active
							 select autoNumberQ).FirstOrDefault();

						if (autoNumberConfigTemp == null)
						{
							continue;
						}

						if (autoNumberConfigTemp.Condition != null)
						{
							isConditioned = true;
							log.Log($"Checking condition for '{autoNumberConfigTemp.Name}':'{autoNumberConfigTemp.Id}' ...");
							var parsedCondition = ParseAttributeVariables(service, autoNumberConfigTemp.Condition, target,
								context.UserId, context.OrganizationId.ToString());
							var isConditionMet = IsConditionMet(service, parsedCondition,
								target.ToEntityReference(), context.OrganizationId.ToString());

							if (isConditionMet)
							{
								log.Log("Condition met for auto-numbering record.");
								autoNumberConfig = autoNumberConfigTemp;
								break;
							}

							log.Log("Condition not met for auto-numbering record.");
						}
						else if (autoNumberConfig == null)
						{
							autoNumberConfig = autoNumberConfigTemp;
						}
					}
				}
				else
				{
					autoNumberConfig = autoNumberConfigTemp;
					isBackLogged = true;
				}
			}

			return PreValidation(service, target, autoNumberConfig, log, isConditioned, isBackLogged);
		}

		private static AutoNumbering GetInlineConfig(string config, Guid userIdForTimezone)
		{
			var inlineConfig = config.Split(new[] { ";;" }, StringSplitOptions.RemoveEmptyEntries)
				.Select(item => item.Trim()).ToArray();

			if (inlineConfig.Length < 2)
			{
				throw new InvalidPluginExecutionException($"Inline config '{config}' is missing one of the config params..");
			}

			return
				new AutoNumbering
				{
					FieldLogicalName = inlineConfig[1],
					FormatString = inlineConfig[0],
					ValidateUniqueString = inlineConfig.Length > 2 && inlineConfig[2] == "true",
					ReplacementCharacters = inlineConfig.Length > 3 ? inlineConfig[3] : null,
					Owner = new EntityReference("systemuser", userIdForTimezone)
				};
		}

		private static AutoNumbering GetBackloggedConfig(IPluginExecutionContext currentContext,
			IOrganizationService service, CrmLog log)
		{
			var xrmContext = new XrmServiceContext(service) { MergeOption = MergeOption.NoTracking };

			var triggerIdGuid = GetTriggerId(currentContext);

			// check backlog if necessary
			if (triggerIdGuid == null)
			{
				log.Log("No trigger ID found.");
				return null;
			}

			var triggerId = triggerIdGuid.ToString();
			log.Log($"Found trigger ID '{triggerId}' in shared variables.");

			log.Log("Retrieving backlog entry ...");
			var triggerBacklog =
				(from backlogQ in xrmContext.AutoNumberingBacklogSet
				 join autonumberQ in xrmContext.AutoNumberingSet
					 on backlogQ.AutoNumberingConfig equals autonumberQ.AutoNumberingId
				 where backlogQ.TriggerID == triggerId
				 select new AutoNumberingBacklog
						{
							Id = backlogQ.Id,
							IndexValue = backlogQ.IndexValue,
							AutoNumberingConfig = backlogQ.AutoNumberingConfig,
							AutoNumberingAsAutoNumberingConfig = autonumberQ
						}).FirstOrDefault();
			log.Log("Finished retrieving backlog entry.");

			if (triggerBacklog == null)
			{
				log.LogWarning($"Couldn't find backlog entry for trigger ID '{triggerId}'.");
				return null;
			}

			log.Log($"Using backlog '{triggerBacklog.Id}' with index {triggerBacklog.IndexValue}.");
			triggerBacklog.AutoNumberingAsAutoNumberingConfig.CurrentIndex = triggerBacklog.IndexValue;

			log.Log("Deleting backlog entry ...");
			service.Delete(triggerBacklog.LogicalName, triggerBacklog.Id);
			log.Log("Finished deleting backlog entry.");

			return triggerBacklog.AutoNumberingAsAutoNumberingConfig;
		}

		private static object GetTriggerId(IPluginExecutionContext currentContext)
		{
			currentContext.SharedVariables.TryGetValue("AutoNumberingTriggerId", out var triggerIdGuid);

			if (triggerIdGuid == null && currentContext.ParentContext != null)
			{
				return GetTriggerId(currentContext.ParentContext);
			}

			return triggerIdGuid;
		}

		// credit: https://community.dynamics.com/crm/b/mshelp/archive/2012/06/12/get-optionset-text-from-value-or-value-from-text
		/// <summary>
		///     This function is used to retrieve the optionset label using the optionset value
		/// </summary>
		/// <param name="service"></param>
		/// <param name="entityName"></param>
		/// <param name="attributeName"></param>
		/// <param name="selectedValue"></param>
		/// <returns></returns>
		internal static string GetOptionsSetTextForValue(IOrganizationService service, string entityName,
			string attributeName, int selectedValue, string orgId)
		{
			var cacheKey = $"GetOptionsSetTextForValue|{entityName}|{attributeName}"
				+ $"|{selectedValue}|{Libraries.Common.Helpers.GetAssemblyName(0)}";
			var attributeCached = CacheHelpers.GetFromMemCache<string>(cacheKey, orgId);

			if (attributeCached != null)
			{
				return attributeCached;
			}

			var retrieveAttributeRequest =
				new
					RetrieveAttributeRequest
				{
					EntityLogicalName = entityName,
					LogicalName = attributeName,
					RetrieveAsIfPublished = true
				};

			// Execute the request.
			var retrieveAttributeResponse = (RetrieveAttributeResponse)service.Execute(retrieveAttributeRequest);

			// Access the retrieved attribute.
			var retrievedPicklistAttributeMetadata = (PicklistAttributeMetadata)retrieveAttributeResponse.AttributeMetadata;
			var optionList = retrievedPicklistAttributeMetadata.OptionSet.Options.ToArray();
			return CacheHelpers.AddToMemCache(cacheKey,
				(from oMD in optionList
				 where oMD.Value == selectedValue
				 select oMD.Label.LocalizedLabels[0].Label).FirstOrDefault(),
				DateTime.Now.AddHours(12), orgId);
		}

		// credit: http://blogs.msdn.com/b/crm/archive/2007/06/18/understanding-crm-metadata-primarykey-and-primaryfield.aspx
		/// <summary>
		///     Retrieve a CRM Entity's primarykey and primaryfield
		/// </summary>
		/// <param name="service"></param>
		/// <param name="entity"></param>
		internal static string GetEntityPrimaryFieldValue(IOrganizationService service, EntityReference entity, string orgId)
		{
			var cacheKey = $"GetEntityPrimaryFieldValue|{entity.LogicalName}|{Libraries.Common.Helpers.GetAssemblyName(0)}";
			var primaryField = CacheHelpers.GetFromMemCache<string>(cacheKey, orgId)
				?? CacheHelpers.AddToMemCache(cacheKey, ((RetrieveEntityResponse)service.Execute(
					new RetrieveEntityRequest
					{
						EntityFilters = EntityFilters.Entity,
						LogicalName = entity.LogicalName
					})).EntityMetadata.PrimaryNameAttribute,
					DateTime.Now.AddHours(12), orgId);

			return service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(primaryField))
				.GetAttributeValue<string>(primaryField);
		}

		internal static int GetNextIndex(AutoNumbering autoNumberConfig, AutoNumbering updatedAutoNumbering)
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

			var currentIndex = autoNumberConfig.CurrentIndex.GetValueOrDefault();

			// if invalid value, reset
			// if updating and not incrementing, then keep index, else increment index
			var index = currentIndex <= 0 ? 1 : currentIndex + 1;

			if (isReset)
			{
				index = resetValue;
			}

			updatedAutoNumbering.ResetDate = resetDate;
			updatedAutoNumbering.LastResetDate = lastResetDate;

			return index;
		}
	}
}
