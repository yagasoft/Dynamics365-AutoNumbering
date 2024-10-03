function EntityLogicalName_OnChange(executionContext)
{
    SetAnchoredExecutionContext(executionContext);
    LoadAdvancedFind('ys_condition', null, 200, 'ys_entitylogicalname');
}
