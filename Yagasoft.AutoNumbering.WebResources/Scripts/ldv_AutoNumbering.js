function OnLoad()
{
    LoadAutoAdvancedFind('ldv_condition', 'ldv_entitylogicalname', 200, function(isValid)
    {
        if (isValid)
        {
            SetupFieldNameAutoComplete(GetFieldValue('ldv_entitylogicalname'), 'ldv_fieldlogicalname', 10, null, true);
        }
        else
        {
            ClearAutoComplete('ldv_fieldlogicalname');
            ClearFieldValue('ldv_fieldlogicalname', true);
        }
    });
}
