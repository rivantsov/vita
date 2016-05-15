# VITA Application Framework
*.NET ORM Framework - DONE RIGHT*

VITA is a framework for creating data-connected .NET applications. It is a full-featured .NET [ORM](http://en.wikipedia.org/wiki/Object-relational_mapping) - and much more than ORM. Automatic schema management, full Linq implementation with query caching, support for CRUD stored procedures, modular application composition, transparent data cache implementation with Linq queries redirection, row-level authorization subsystem. Supports data models distributed over multiple databases. 
Works with MS SQL Server 2008/2012, MS SQL Compact Edition, MySql, PostgreSQL, SQLite.

## Highlights
* **Entities are defined as .NET interfaces** - minimum coding required. Just _'string Name {get;set;}'_ for a property - compact and clear. 
* **Entities are self-tracking** - they maintain original and modified property values, automatically register for update. 
* **Database model is automatically created/updated from entity model**. Existing data is preserved. Database schema follows c# code. 
* *Database keys and indexes* are defined through entity attributes in c# - you can define most of the database artifacts in c# code.
* **Automatically generated CRUD stored procedures**. Alternatively, application can use direct SQL queries.
* **Support for one-to-many and many-to-many relationships.** Foreign keys are automatically inferred from c# entity references. Properties which are lists of related entities are automatically "filled-up" on first read. 
* **Full LINQ support** for database querying. And not only SELECTs - you can use *LINQ expressions to execute INSERT, UPDATE and DELETE* statements.
* **Database views** generated from LINQ queries, including materialized views with indexes.
* **DB-first approach is fully supported** - VITA provides a code generator that produces the complete source code for the solution from the existing database schema. 
* **Full-featured data cache** - you can specify a set of entity types (database tables) that must be cached - VITA loads the data in memory, and all subsequent data queries (including dynamic LINQ queries) are served from this cache. Additional 'sparse' cache holds most recently used records/entities from tables that are too big to be cached entirely.
* **Batched operations** - multiple database update/delete/insert commands are combined in a single multi-line command executed in one round-trip to the database.  
* **Compiled LINQ Query Cache** - dynamic LINQ queries are compiled into SQL on the first use, and later served from the query cache, avoiding the overhead of SQL translation. 
* **Built-in Role-based Authorization framework** - access permissions are configured at table/row/field level at application startup. During application execution all read/write operations by the current user are checked automatically (and transparently, behind the scene) by VITA.  
* **Component Packaging Technology** - with VITA we have a way to pack a set of entities/tables with the surrounding code into a self-contained component - entity module. Entity modules can cover specific areas of functionality, can be independently developed, tested and distributed, just like Windows Control libraries. The application is then assembled from independent modules, each covering specific part of functionality. 
* **Standard modules** - VITA comes with a number of pre-built modules: ErrorLog, UserLogin, TrasactionLog, etc. 
* **Web API stack integration**. Fully integrated with Web API technology stack, provides numerous benefits for easy creation of fast and reliable RESTful data services. 
* **Full support for identity columns and auto-generated GUIDs**
* **Computed properties**.
* **Entities support INotifyPropertyChanged interface** - readily usable in data binding and MVVM solutions.

### System Requirements
* .NET 4.5, Visual Studio 2015. 
* MS SQL Server 2008/2012/2014; MS SQL CE 4.0; MySql 5.5, PostgreSQL 9.2, SQLite


