function EntityLogicalName_OnChange(executionContext)
{
    SetAnchoredExecutionContext(executionContext);
    LoadAdvancedFind('ldv_condition', null, 200, 'ldv_entitylogicalname');
}
