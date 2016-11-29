using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Data.Driver {

  public enum DbServerType {
    MsSql,
    SqlCe,
    MySql,
    Postgres,
    Sqlite,
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
    NoIndexOnForeignKeys = 1 << 11, // server does NOT automatically create index on foreign keys, so these should be created explicitly
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
  }


}
