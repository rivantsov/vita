using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading;

using Vita.Entities.Utilities; 
using Vita.Entities.Model; 
using Vita.Entities;

namespace Vita.Data.Upgrades {

  public enum UpgradeStatus {
    None,
    HigherVersionDetected,
    NotAllowed,
    NoChanges,
    ChangesDetected,
    Applied,
    Failed,
  }

  public enum DbUpgradeMethod {
    /// <summary>Automatic, updating from code at application startup. Typical for development installations. </summary>
    Auto = 0,
    /// <summary>Manually, using model update tools (DIME). Typical for staging/shared and production environments.
    /// </summary>
    Manual = 1,
  }

  public enum DbObjectChangeType {
    Add,
    Rename, //table or column
    Modify,
    Drop,
  }

  [Flags]
  public enum DbScriptOptions {
    None = 0,
    NewColumn = 1,
    // for new columns without defaults we create them as nullable 
    ForceNull = 1 << 1,
    // Finalizing column after adding it
    CompleteColumnSetup = 1 << 2,
    // Used by Oracle
    NullabilityChange = 1 << 3,
  }

  /// <summary>Identifies script type and determines the default execution order of SQL scripts for changing the DB model. </summary>
  public enum DbScriptType {
    ScriptInit,
    MigrationStartUpgrade,

    DatabaseAdd, 
    RoutineDrop,
    RefConstraintDrop,
    IndexDrop, //indexes, primary keys
    TableConstraintDrop,
    ViewDrop, 

    SchemaAdd,

    TableRename,
    ColumnRename,
    TableAdd,

    ColumnAdd,
    ColumnCopyValues,  // used in renaming by [add new + copy values + delete old] 
    ColumnModify,
    MigrationMidUpgrade, 
    ColumnInit, //special action for initializing columns (added or modified) from NULL values
    ColumnSetupComplete, //action to set new/existing columns from nullable to NonNullable

    ViewAdd,
    PrimaryKeyAdd,
    IndexAdd,
    RefConstraintAdd,
    CustomTypeAdd, //must be before RoutineAdd
    RoutineAdd,
    
    ColumnDrop,
    TableDrop,
    CustomTypeDrop,

    SequenceDrop,
    SequenceAdd,
    
    Grant,
    SchemaDrop,
    MigrationEndUpgrade, 
    DatabaseDrop,
    Completed,

  }

}
