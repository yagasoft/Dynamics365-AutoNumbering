# Dynamics365-AutoNumbering

[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/yagasoft/DynamicsCrm-AutoNumbering?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

---

A CRM solution that gives a lot of flexibility in creating any pattern required for auto-numbering.

### Features

  + All features of the CRM Parser ([CRM Parser](https://github.com/yagasoft/Dynamics365-CrmTextParser))
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

`Test-{@this.createdon$date(`hh:mm`)??`NO_DATE`}-{$rand(5,`un`)}-{$now$date(`yyyy`)}-{$sequence}-{$inparam(3)}`

#### _Input parameters_
  + Current index: 5
  + Padding: 3
  + Executing user in Cairo at 9AM and server in London 7AM
  + String 'PA;RA;M' as input parameter to the WF step
  
#### _Result numbering_

`Test-09:00-YAM76-2015-005-M`

### Guide

Please check the 'docs' folder for a guide PDF.

### Dependencies

  + Common.cs
    + Can be found in the [DynamicsCrm-Libraries](https://github.com/yagasoft/DynamicsCrm-Libraries) repository
  + YS Common solution ([Dynamics365-YsCommonSolution](https://github.com/yagasoft/Dynamics365-YsCommonSolution))
		
## Changes
+ Check Releases page for the later changes
#### _v4.2.1.1 (2021-04-21)_
+ Changed: moved to a managed solution with dependency on a new base solution
#### _v4.1.1.1 (2019-02-27)_
+ Changed: moved to a new namespace
#### _v3.2.2.1 (2018-12-19)_
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
**Copyright &copy; by Ahmed Elsawalhy ([Yagasoft](https://yagasoft.com))** -- _GPL v3 Licence_
