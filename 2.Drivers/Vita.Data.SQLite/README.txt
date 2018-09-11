
1. Aug 2018. Do NOT try to upgrade to 2.1 of MS SQLite package!
   Microsoft.Data.Sqlite package, latest version 2.1 - does not work; suddenly in the middle, after many queries executed OK, 
   suddenly queries with 'WHERE id=@P0' stop working. Switched back to 2.01 and everything works OK. 