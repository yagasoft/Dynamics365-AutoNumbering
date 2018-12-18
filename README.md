# DynamicsCrm-AutoNumbering

[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/yagasoft/DynamicsCrm-AutoNumbering?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

### Version: 3.2.1.1
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
  + Create different index sequence per field value

### Example

#### _Format String_

`Test-{{createdon}??{createdon@hh:mm}::NO_DATE}-{!rand!$un:5}-{!now!yyyy}-{!index!casetypecode}-{!param!3}`

#### _Input parameters_
  + Current index: 5
  + Padding: 3
  + Executing user in Cairo at 9AM and server in London 7AM
  + String 'PA;RA;METER' as input parameter to the WF step
  
#### _Result numbering_

`Test-09:00-Ahmed-4AM7Z-2015-005-METER"`

### Guide

Please check the 'docs' folder for a guide PDF.

### Dependencies

  + Common.cs
    + Can be found in the [DynamicsCrm-Libraries](https://github.com/yagasoft/DynamicsCrm-Libraries) repository
  + Generic Base solution ([DynamicsCrm-BaseSolution](https://github.com/yagasoft/DynamicsCrm-BaseSolution))
  + CRM Logger solution ([DynamicsCrm-CrmLogger](https://github.com/yagasoft/DynamicsCrm-CrmLogger))
		
## Changes

#### _v3.2.1.1 (2018-12-18)_
+ Changed: upgraded to the new placeholder system
#### _v3.1.1.1 (2018-12-05)_
+ Added: index streams
+ Improved: use more advanced placeholders
#### _v2.2.1.1 (2018-12-04)_
+ Added: automatic registration option for the Create message plugin step
#### _v2.1.1.1 (2018-09-05)_
+ Added: Web Resources to project
+ Changed: cleaned the project of obsolete components

---
**Copyright &copy; by Ahmed el-Sawalhy ([Yagasoft](http://yagasoft.com))** -- _GPL v3 Licence_
