﻿using System;
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
  public enum DbFeatures: Int64 {
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

    // If set, foreign keys are indexed by default; 
    // if otherwise, DbOptions enum has a flag to auto-create indexes on foreign keys
    ForeignKeysAutoIndexed = 1 << 11,

    OrderedColumnsInIndexes = 1 << 12, //supports ordered columns in indexes
    IncludeColumnsInIndexes = 1 << 13, //supports include columns in indexes
    FilterInIndexes = 1 << 14,         // supports filters in indexes
    Views = 1 << 15,         // supports views
    MaterializedViews = 1 << 16,
    InsertMany = 1 << 17, // insert many rows in one Insert SQL; supported by all except Oracle

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

    ExceptOperator = 1L << 32,
    IntersectOperator = 1L << 33,
  }



}
