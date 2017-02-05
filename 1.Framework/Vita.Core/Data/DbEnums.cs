using System;
using System.Collections.Generic;
using System.Data;

namespace Vita.Data {

  /// <summary>DB option flags. </summary>
  [Flags]
  public enum DbOptions {
    /// <summary>Empty set.</summary>
    None = 0,

    /// <summary>Use stored procedures for CRUD access.</summary>
    UseStoredProcs = 1,

    /// <summary>Create referential integrity constraints in database. </summary>
    UseRefIntegrity = 1 << 1,

    /// <summary>Pluralize table names, ex: entity IProduct -> table Products. </summary>
    PluralizeTableNames = 1 << 2,

    /// <summary>Ignore table name case.</summary>
    IgnoreTableNamesCase = 1 << 3,

    /// <summary>For servers that support array parameters (MS SQL, Postgres), do not use this facility 
    /// but inject array values directly into SQL as literals. </summary>
    ForceArraysAsLiterals = 1 << 4,

    /// <summary>Instructs system to create indexes on all foreign keys if server does not do it automatically like SQL Server.</summary>
    AutoIndexForeignKeys = 1 << 8,

    /// <summary>Use batch mode for update operations.</summary>
    UseBatchMode = 1 << 9,

    /// <summary> Db model is shared between data sources with the same instance of DbModelConfig.</summary>
    ShareDbModel = 1 << 10,

    /// <summary> If set, the schema name is added as prefix to table name; for ex: sch_MyTable.
    /// Useful for servers that do not support schemas. </summary>
    AddSchemaToTableNames = 1 << 11, 

    /// <summary> Default value.</summary>
    Default = UseStoredProcs | UseRefIntegrity | ShareDbModel,

  }

  /// <summary>Defines values for database model (db schema) update mode.</summary>
  public enum DbUpgradeMode {
    /// <summary>Never update database model. Suitable for production servers.</summary>
    Never,
    /// <summary>Update database model only on non-production databases.</summary>
    /// <remarks>The database instance type is saved in the DbInfo table defined in the DbInfo module.</remarks>
    NonProductionOnly,
    /// <summary>Always update database model.</summary>
    Always 
  }

  /// <summary>Defines option flags for database model update operations.</summary>
  [Flags]
  public enum DbUpgradeOptions {
    /// <summary>Empty value, no options specified.</summary>
    None = 0,

    /// <summary>Update tables in database update process.</summary>
    UpdateTables = 1,
    /// <summary>Update indexes in database update process.</summary>
    UpdateIndexes = 1 << 1,
    /// <summary>Update stored procedures in database update process.</summary>
    UpdateStoredProcs = 1 << 2,
    /// <summary>Update DB Views in database update process.</summary>
    UpdateViews = 1 << 3,
  
    /// <summary>Drop tables and indexes that have no matching objects in entity model.</summary>
    DropUnknownObjects = 1 << 8,

    /// <summary>Default value - union of <c>UpdateTables</c>, <c>UpdateIndexes</c> and <c>UpdateStoredProcs</c>.</summary>
    Default = UpdateTables | UpdateIndexes | UpdateStoredProcs | UpdateViews,
  }

  public enum StringCaseMode {
    CaseSensitive,
    CaseInsensitive,
  }
}//ns
