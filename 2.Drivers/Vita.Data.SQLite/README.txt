README for VITA SQLite driver
1. Add the following value to connection string to support foreign keys: 
   foreign keys=true
2. You need to directly reference the System.Data.SQLite.Core nuget package in your app ('install it into the app'). Notice that SQLite
   package is referenced by unit test projects. 
   (For other drivers like MySql, it is enough to reference VITA driver assembly). SQLite managed provider uses unmanaged/interop assemblies, 
   so to make build process correctly copy these interop assemblie(s), you need to reference the package from your 'top' project. 