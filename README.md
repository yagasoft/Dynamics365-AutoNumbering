# DynamicsCrm-AutoNumbering
### Version: 1.1
---

A CRM solution that gives a lot of flexibility in creating any pattern required for auto-numbering.

### Features

  + String, date, parameter, and attribute patterns
  + Run numbering on a condition
  + Random strings, with number-to-letter ratios, and "start with a letter" flag
  + Optional numbering sequence, with padding
  + Reset interval -- periodic or one-time reset -- for numbering sequence
  + Locking when busy to avoid duplicate indexes
  + Option to use plugin instead of workflow step, which allows the generation of numbering for entities that lock after the operation
  + Option to validate unique generated string
  + Option to generate without updating a record (return the generated string only)
  + Support for plugin step inline configuration
  + Use a backlog to avoid long DB locks
    + The solution reserves an index, and if a rollback happens, the index is saved for future use by another run
    + This might cause out-of-order indices

### Example

"Test-{?{$createdon@hh:mm}::NO_DATE}-{!un-5}-{@yyyy}-{index}-{param3}"
With current index 5, padding 3, the user in Cairo (9AM) and server in London (7AM), and 'PA;RA;METER' as input parameter to the WF step, the result numbering string: "Test-09:00-Ahmed-4AM7Z-2015-005-METER"

### Guide

Please check the 'docs' folder for a guide PDF.
I will post a complete guide soon.

### Dependencies

  + Common.cs
    + Can be found in the DynamicsCrm-Libraries repository
  + Generic Base solution
  + CRM Logger solution

---
**Copyright &copy; by Ahmed el-Sawalhy ([YagaSoft](http://yagasoft.com))** -- _GPL v3 Licence_
