*See prior history in the [Code Changes History](https://github.com/rivantsov/vita/wiki/Code-changes-history) page in Wiki tab of this repo.*
## Version 4.1.0 July 9, 2024. Minor update
* Fixed issues #225, #226 - using expressions in Group-By.
* Upgraded System.Text.Json to latest. 

## Version 3.6. Sept 11, 2023. Minor update
* MS SQL driver, batch execution. Added to batch text at start: SET XACT_ABORT ON -- to stop execution on any error and abort trans.
* Deprecated session option EnableSmartLoad, now enabled by default, new opton DisableSmartLoad to disable it explicitly
* SmartLoad implementation: The select-from-multiple style - 'Select ... (where id in ())' - is used only if there's more than 1 sibling parent entity. Seems to be faster when there's only one ID in the list. 
* Upgraded DB provider packages to latest.  

## Version 4.0. June 30, 2024. Major update
* Removed deprecated projects - Vita.Web and Vita.Modules. This functionality is no longer relevant. Web API implementation with ASP.NET Core is quickly advancing (so trouble to keep up the examples). On the other hand, there are plenty of guides and videos out there on how to implement it correctly. Login is better be implemented nowadays through external services - in the cloud (Azure Entra) or thru external services like Okta. Storing logins and password hashes in database is not a good idea. 
* Upgraded package references, db provider references. See Readme for Vita.Data.Postgres about trouble with Npgsql (not upgraded). 
* Fixed 'date' db type for Postgres, now works OK
* Looked at issue #225 (Views does not group by dateTime.Date); turns out it is not a simple bug, but a serious flaw in Linq-to-SQL engine, requires more work; no fix for now, sorry. 
* Removed generating nuget tool package for VitaDbTool (cmd utility to generate entities from db), for now. Turns out it is quite big (26 Mb), will find a way to package it better.  

## Version 4.1. July 9, 2024. 
* Using Expressions in GroupBy fixed - issue #225
 
## Version 4.1. July 9, 2024. 
* Minor fix in db-first entity generator
* Postgres - upgrade of npgsql provider to latest (old version was declared vulnerable)
* Postgres - enhancements/fixes for DateTime/Timestamp db types. Both DateTime and DateTimeOffset .net types are now mapped to 'timestamp with time zone' Db type (which has NO timezone actually) - see here for details: https://www.npgsql.org/doc/release-notes/6.0.html#timestamp-rationalization-and-improvements , https://www.roji.org/postgresql-dotnet-timestamp-mapping
* Search extensions - added ISearchParams interface (identical to SearchParams class); all search helpers use this interface now. This makes it easier to implement your own custom SearchParams types - implement interface instead of using the fixed base class.  
