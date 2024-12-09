*See prior history in the [Code Changes History](https://github.com/rivantsov/vita/wiki/Code-changes-history) page in Wiki tab of this repo.*

## Version 4.4. Dec 9, 2024. 
* Async data access, methods: ToListAsync, CountAsync, FirstOrDefaultAsync, etc.


## Version 4.3. Dec 7, 2024. 
* Patch, bug fix for #240: https://github.com/rivantsov/vita/issues/240   - fixed SQL cache key for list properties

## Version 4.2. Aug 24, 2024. 
* Added Date (Hour, Minute) SQL functions to all providers - fixes unit tests
* Minor fix in db-first entity generator
* Postgres - upgrade of npgsql provider to latest (old version was declared vulnerable), fixed the incompatibility. Note: staying with Legacy behavior for timestamps, DateTime is still mapped to 'timestamp without time zone' type. The 'with time zone' db type behaves weird, see more here: https://www.npgsql.org/doc/release-notes/6.0.html#timestamp-rationalization-and-improvements , https://www.roji.org/postgresql-dotnet-timestamp-mapping
* Search extensions - added ISearchParams interface (identical to SearchParams class); all search helpers use this interface now. This makes it easier to implement your own custom SearchParams types - implement interface instead of using the fixed base class. 
* Option to use sequential GUIDs (time-based, V7). Added EntityApp.GuidFactory() method/field. In some cases using sequential Guids is preferrable in databases - when your Primary Key (Guid) is also your clustered index. (this is always the case in MySql and Oracle). To use time-based Guids in Vita, set the Entity.GuidFactory field to your custom method. Support for these is coming in .NET 9. For now BooksApp uses UUIDNext package, see BooksEntityApp.cs for an example.

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

