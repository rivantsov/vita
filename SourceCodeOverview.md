*Exploring VITA Application Framework* 
The best way to approach the framework and understand what it offers is to explore the unit tests. 
The unit tests are included in the VitaAll.sln solution file, under UnitTests folder. The Vita.UnitTest.Vs
project contains some very basic regression tests. Books samples test project contains many code snippets 
illustrating various aspects of the framework - basic CRUD capabilities, LINQ engine, entity caching, 
authorization framework, SQL logging and others. Inspect the source code to see how everything is done. 

*Building the Solution*
Start Visual Studio 2019 and open the VitaCoreAll.sln solution file.   
Build the solution. The solution should build without errors. 

*Running the Test projects*
Unit test projects can be run in 2 modes:
  1. As traditional unit tests through Test Explorer window in VS. In this mode the tests run for one DB server, 
    MS SQL Server by default. You can change the server by modifying the appSettings.json file in unit test project. 
  2. As a console application for all supported database servers - just set the test project as 'Startup Project' 
    and run it (F5). The set of servers to run against is configured in the appSettings.json files. 

*Before you run the tests - you need to create databases and adjusting the connection strings*
  1.MS SQL Server: create databases VitaTest, VitaBooks, VitaBooksLogs (the latter is used in WebTests only)
  2. SQLite: the tests create local database file at start automatically, not need to do anything manually.
  3. Local MySql installation:
    - Add the following options to sql-mode variable in my.ini file (in C:\ProgramData\MySQL\MySQL Server [5.6]) 
         ANSI_QUOTES
    - Connection string must have ' Old Guids=true' to properly handle Guids
    - MySql treats schema/database terms interchangeably, so you will see 'schemas' 
    created by unit tests as databases in local server.
  4. Postgres: create empty databases: VitaTest, VitaBooks. You need to run the following script in SQL window:
       CREATE EXTENSION "uuid-ossp";
   (it is an extension for generating GUIDs in LINQ-insert commands). Also create login: testuser/pass

! Important: before running the unit tests turn off the 'Break on CLR exceptions' setting 
   in Visual Studio (clear all checkboxes in Debug/Exceptions dialog in main VS Menu) 
  Tests and samples throw and handle exceptions, so stopping would disrupt normal test execution. 

Run the units tests using - 
  1. VS Test Explorer window - right-click on the test group and select 'Run/Debug selected tests'. 
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
    - Vita - core assembly/package, entity CRUD operations
    - Vita.Web - assymbly/package implementing integration with WebApi/Asp.NET-core.
    - Vita.Tools - tools library, used by tools/tests internally, not published as package

  2. Drivers - contains database drivers (providers) for different database servers. The following servers are supported: 
     MS SQL Server, MySql, Postgres, Oracle, SQLite. Each project is published as a separate nuget package. 
  
  3. Modules
     Vita.Modules.Login - implements standard Login functionality.
     Vita.Modules.Login.Api - WebApi project implementing REST API for login functionality.
     Vita.Modules.Logging - assembly/package implementing logging to db functionality. Logging to db is demo-ed 
       in WebTests project. 
     Vita.Modules.Legacy - a number of legacy entity modules (EncryptedData, Party, etc). These packages are provided 
       for backward compatibility and support of existing applications; the package, while maintained currently,
       will not be in active development. 

  4. Samples - contains a sample application: 
      - Vita.Samples.BookStore - an online Book store, a class library containing data and business logic. 
         This assembly and app is used in Extended unit tests project.
      - Vita.Samples.BookStore.SampleData - sample data generator for the bookstore
      - Vita.Samples.BookStore.Api - an ASP.NET Core project, defines API controllers implementing RESTful API for a 
        bookstore application

  5. Tools folder
    - vdbtool - command line tool. Provides the following:
        * generating entity definitions from the database objects (DB-first approach). 
        * generating DB update scripts by comparing entity definitions and target database. Scripts may be used
		   to update production databases in manual mode.
      vdbtool.exe is a console application, it uses a config file for all parameters. You designate the name 
	  of the cfg file to use as a command-line parameter:
            vdbtool.exe dbfirst /cfg:books.vdb.cfg
     Several sample cfg files are included with the project. For MS Sample databases (AdventureWorks, Northwind) -
	   you can install these databases before you run the entity generator - you can download installation scripts
	   from MS download site. 

  6. UnitTest - a set of unit-test projects (for purists - these are more like integration tests) 
    - Vita.Testing.BasicTests - a set of unit tests for basic VITA functionality. 
    - Vita.Testing.ExtendedTests - advanced tests using BookStore sample model/database.
    - Vita.Testing.WebTests - tests for RESTful API of bookstore application; uses Arrest nuget package as REST client 
        to hit API endpoints. 

