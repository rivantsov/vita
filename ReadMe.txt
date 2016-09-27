*Exploring VITA Application Framework* 
The best way to approach the framework and understand what it offers is to explore the unit tests. 
The unit tests are included in the VitaAll.sln solution file, under UnitTests folder. The Vita.UnitTest.Vs
project contains some very basic regression tests. Books samples test project contains many code snippets 
illustrating various aspects of the framework - basic CRUD capabilities, LINQ engine, entity caching, 
authorization framework, SQL logging and others. Inspect the source code to see how everything is done. 

*Building the Solution*
Start Visual Studio 2013 as Administrator and open the VitaAll.sln solution file. VITA uses nuget packages
 (for WebApi functionality). The dependent binaries should be restored automatically when you run the build.  
Build the solution. The download includes external libraries used by projects (data providers for different servers), 
so the solution should build without errors. 

*Running the Test projects*
Note that Vita.UnitTests.Common project is a utility library used in other test projects, not unit test container by itself. 
Unit test projects can be run in 2 modes:
  1. As traditional unit tests through Test Explorer window in VS. In this mode the tests run for MS SQL Server 2008. 
    You can change the target by modifying the App.config file in unit test projects. 
  2. As a console application for all supported database servers - just select the project as 'Startup Project' 
    and click the Run button in Visual Studio (F5). The set of servers to run against is configured in the App.config files. 

*Before you run tests - creating databases and adjusting connection strings*
For any (or all) of the database servers you should adjust connection strings in App.config file in the corresponding 
unit test project. You should also precreate test databases on the servers (except MS SQL CE and MySql). 
  1.MS SQL Server: create databases VitaTest, VitaBooks, VitaBooksLogs
  2. MS SQL Compact and SQLite: these tests use an empty database file included in test project; 
    the file is copied to the bin folder on project compile and the copy is used as a test database
  3. Local MySql installation:
    - Add the following options to sql-mode variable in my.ini file (in C:\ProgramData\MySQL\MySQL Server 5.6) 
         ANSI_QUOTES
    - Connection string must have ' Old Guids=true' to properly handle Guids
    - MySql treats schema/database terms interchangeably, so you will see 'schemas' 
    created by unit tests as databases in local server.
  4. Postgres: create empty databases: VitaTest, VitaBooks. You need to run the following script in SQL window:
       CREATE EXTENSION "uuid-ossp";
   (it is extension for generating GUIDs in LINQ-insert commands). Also create login: testuser/pass

   SQLite - you need to install run-time components using proper download from here: 
   http://system.data.sqlite.org/index.html/doc/trunk/www/downloads.wiki

! Important: before running the unit tests turn off the 'Break on CLR exceptions' setting 
   in Visual Studio (clear all checkboxes in Debug/Exceptions dialog in main VS Menu) 
  Tests and samples throw and handle exceptions, so stopping would disrupt normal test execution. 

Run the units tests using - 
  1. VS Test Explorer window - right-click on the test group and select 'Run selected tests'. 
  2. As a console applications - mark the test project as 'Startup project'; open app.config file and 
    adjust the value of 'ServerTypesForConsoleRun' key - leave only server types that you have installed.
	Run the test project. The console window will appear and you will see the progress report as the system runs
	the tests for all/some supported server types. 
Once the tests complete, open Database browser tool of your choice (ex: MS SQL Management Studio, SMS) 
and browse the generated data in the database tables. All database objects are created automatically 
at unit tests startup, as part of the automatic schema update functionality. The initial data (for Books sample)
are generated in the SampleDataGenerator in Books model project; some working data is added/modified 
in unit tests run. Inspect the source code for unit tests, see how things are done. 
Note: We no longer provide an NUnit version of test projects, as it is possible now to run unit tests 
outside of the Visual Studio, as a console application.  

Solution Structure Overview     
In VS Solution Explorer there is a number of folders in the main solution: 
  1. Framework - core components of VITA. Includes the projects:
    - Vita.Core - core entity model, entity CRUD operations
    - Vita.Web - web-related functionality. 
  2. Drivers - contains database drivers (providers) for different database servers. The following servers are supported: 
     MS SQL 2008/2012; MS SQL Compact Edition (SQL CE); MySql; Postgres; SQLite. Driver for MS SQL Server is included into core VITA assembly. 
  3. Modules - contains Vita.Modules project. This project is a container for a number of standard modules that are provided out of the box. 
       Contains useful functionality that can be easily imported into your application.
  4. Samples - contains a sample application: 
      - Vita.Samples.BookStore - an online Book store, a class library containing data and business logic. 
         This assembly and app is used in Extended unit tests project.
      - Vita.Samples.BookStore.SampleData - sample data generator for the bookstore
      - Vita.Samples.BookStore.UI - a Web app based on AngularJs frontend, with api controllers serving data only as Json. 
        The initial set of books in catalog is imported from GoogleBooks API. 
      - Vita.Samples.OAuthDemoAp - a desktop application demoing OAuth 2.0 implementation for several popular sites supporting the protocol
  5. UnitTest - a set of unit-test projects. 
    - Vita.UnitTests.Common - a utility library, not unit test container. 
    - Vita.UnitTests.Basic - a set of unit tests for basic VITA functionality. 
    - Vita.UnitTests.Extended - advanced tests using BookStore sample model/database.
    - Vita.UnitTests.Web - Web functionality tests; uses book store sample data service
  6. Tools folder
    - Vita.ToolLib - tooling library
    - vdbtool - command line tool. Provides the following:
        * generating entity definitions from the database objects (DB-first approach). 
        * generating DB update scripts by comparing entity definitions and target database. Scripts may be used
		   to update production databases in manual mode.
      vdbtool.exe is a console application, it uses a config file for all parameters. You designate the name 
	  of the cfg file to use as a command-line parameter:
            vdbtool.exe dbfirst /cfg:books.vdb.cfg
     Several sample cfg files are included with the project. For MS Sample databases (AdventureWorks, Northwind) -
	  you should have these databases installed before you run the entity generator - you can download installation scripts
	  from MS download site. 

