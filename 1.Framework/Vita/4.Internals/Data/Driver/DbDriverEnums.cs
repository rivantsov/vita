using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Data.Driver {

  public enum DbServerType {
    MsSql,
    MySql,
    Postgres,
    SQLite,
    //Not supported yet
    Oracle,
    DB2,
    Other,
  }

  [Flags]
  public enum DbFeatures {
    None = 0,

    Schemas = 1,
    ReferentialConstraints = 1 << 1,
    Paging = 1 << 2,
    StoredProcedures = 1 << 3,
    OutputParameters = 1 << 4,
    DefaultParameterValues = 1 << 5,
    ArrayParameters = 1 << 6,
    HeapTables = 1 << 7, //tables with no clustered indexes

    DeferredConstraintCheck = 1 << 8,
    DefaultCaseInsensitive = 1 << 9,
    ClusteredIndexes = 1 << 10,
    ForeignKeysAutoIndexed = 1 << 11, // Foreign keys are indexed by default(always). DbOptions enum has a flag to auto-create indexes on foreign keys
    OrderedColumnsInIndexes = 1 << 12, //supports ordered columns in indexes
    IncludeColumnsInIndexes = 1 << 13, //supports include columns in indexes
    FilterInIndexes = 1 << 14,         // supports filters in indexes
    Views = 1 << 15,         // supports views
    MaterializedViews = 1 << 16,

    ForceNullableMemo = 1 << 17, //Memo columns must be nullable (SQL CE)
    NoMemoInDistinct = 1 << 18, //Memo columns are not allowed in DISTINCT clause (SQL CE)

    TreatBitAsInt = 1 << 19, //MS SQL - 'bit' type values/expr should not be treated as bool but as ints

    BatchedUpdates = 1 << 20,
    OutParamsInBatchedUpdates = 1 << 21,
    Sequences = 1 << 22, //MS SQL, Postgres

    SkipTakeRequireOrderBy = 1 << 23,  //MS SQL, SqlCe, Postgres
    AllowsFakeOrderBy = 1 << 24, // 'ORDER BY (SELECT 1)' - MS SQL, Postrgres; SQL CE does not allow this

    UpdateFromSubQuery = 1 << 25, // supports UPDATE statement with FROM clause with SELECT subquery (MS SQL and Postgres)

    ForeignKeyMustTargetPrimary = 1 << 26, // MS SQL, MySql, Postgres allow targeting any unique key, not only PK
    // Server preserves comment lines in stored procs and views (MS SQL); 
    // VITA uses special comment lines in sources to save source hash (to detect need to update view/proc definition), and to save descriptive tag
    ServerPreservesComments = 1 << 27,  
  }

  [Flags]
  public enum DbTypeFlags {
    /// <summary>No flags. </summary>
    None = 0,

    Unlimited = 1,

    /*
    /// <summary>Indicates that the storage type is default for CLR type (ColumnOutType)</summary>
    IsDefault = 1 << 1, 
    */

    /// <summary>User-defined type (MS SQL Server)</summary>
    UserDefined = 1 << 4,
    /// <summary>Type is array.</summary>
    Array = 1 << 5,

    ObsoleteType = 1 << 6,

    /*
    /// <summary>Is default for CLR type (ColumnOutType property). </summary>
    IsDefaultForClrType = 1 << 10,
    /// <summary></summary>
    /// <summary>Can handle unlimited (memo/blob) fields (text or binary). </summary>
    SupportsUnlimited = 1 << 11,

    /// <summary>Is specialization of a db type, for specific CLR type. Ex: binary(16) for Guid</summary>
    IsSubType = 1 << 12, 
    */
  }



}
