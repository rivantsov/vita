# VITA Application Framework  

VITA is a full-featured .NET [ORM Framework](http://en.wikipedia.org/wiki/Object-relational_mapping).

It supports multiple database servers: Microsoft SQL Server, MySql, PostgreSQL, Oracle, SQLite.

It provides full LINQ support, complex data models, one-to-many and many-to-many relations, lazy loading, batched transactional updates and many other features expected from a modern full-featured ORM. 

One of the distinguished features is automatic database schema handling. With code-first model, you change your c# code, and database automatically updates to match the model. You can define most of the database artefacts directly in c# code: tables, indexes, referential constraints, specific data types for columns, etc.

VITA implements integration with a GraphQL Server based on [NGraphQL](https://github.com/rivantsov/ngraphql) framework. It provides robust handling for batched loading known as an (N + 1) problem in GraphQL.  
 
## Documentation and samples
See [Wiki pages](https://github.com/rivantsov/vita/wiki) of this repository.

The source code contains a sample BookStore application. The test projects contain many examples of data access using the framework. 

## Nuget packages
Binaries are distributed as Nuget packages. 

## System Requirements
* .NET Standard 2.0, Visual Studio 2019; .NET Core 3.1 for test and sample projects 
* MS SQL Server 2012+; MySql, PostgreSQL, Oracle, SQLite
