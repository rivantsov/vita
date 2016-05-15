Running the Test projects
Note: Vita.UnitTests.Common is a utility library used by test projects, not a unit test container.
All unit test projects can be run with the any of the database servers supported by VITA. Each unit test project is in fact a console application containing test classes with tests. 
You can run it from VS Test Explorer for a single selected db server type - specify it in app.config file. 
If you run test project directly as a console app, you can run all tests for multiple db servers - the list is again specified in the app.config file. 
You must modify corresponding connection strings in app.config. For MS SQL and Postgres you must create the following databases on the target server: 
 1. VitaTest - used in general unit tests
 2. VitaBooks - used in books sample and unit test
SQL CE uses a local database file (sdf file, part of the project) - it is copied to the binary folder before running the test.
For MySql, you do not need to pre-create anything on the local server - MySql treats schema/database terms interchangeably, so you will see 'schemas' created by unit tests as databases in local server. 
After you run the unit test projects, look at the databases - you'll see all newly-created tables, keys, indexes, stored procedures. Look at the db SQL log file in the bin folder - it contains all SQLs sent to the database. For Console mode the errors.log file contains full exception details for all errors encountered.  
