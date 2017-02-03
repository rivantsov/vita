using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Data.Driver;
using Vita.Data.Model;

namespace Vita.Data.SqlCe {
  public class SqlCeDbModelLoader : DbModelLoader {
    public SqlCeDbModelLoader(DbSettings settings, SystemLog log) : base(settings, log) { }

    public override DbTable GetSchemas() {
      return EmptyTable;
    }
    public override DbTable GetDatabases() {
      return EmptyTable;
    }
    public override DbTable GetRoutines() {
      return EmptyTable;
    }

    //This is an exception - standard INFORMATION_SCHEMA does not provide INDEXES view. SQL CE does provide it - nice! - but there's a problem.
    // Indexes view is in fact a list of index columns; so we have to use DISTINCT
    // NOTE schema is ignored - SQL CE does not support schemas
    public override DbTable GetIndexes() {
      var sql = @"
SELECT DISTINCT table_schema, table_name, index_name, primary_key, [unique],  0 as [clustered], '' as [FILTER_CONDITION]
FROM INFORMATION_SCHEMA.Indexes
ORDER BY table_name,  index_name
";
      return ExecuteSelect(sql);
    }
    public override DbTable GetIndexColumns() {
      var sql = @"
SELECT table_schema, table_name, index_name, column_name, ordinal_position as column_ordinal_position, 
   (collation - 1) as is_descending
FROM INFORMATION_SCHEMA.Indexes
ORDER BY table_name, index_name, ordinal_position
";
      return ExecuteSelect(sql);
    }

    //Columns: TABLE_CATALOG	TABLE_SCHEMA	TABLE_NAME	TABLE_TYPE  (table_type: "TABLE") 
    public override DbTable GetTables() {
      var sql = "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='TABLE' ";
      return ExecuteSelect(sql);
    }

    //SQL CE does not support views
    public override DbTable GetViews() {
      return new DbTable(); 
    }

    //Columns: CONSTRAINT_CATALOG, CONSTRAINT_SCHEMA, CONSTRAINT_NAME, UNIQUE_CONSTRAINT_CATALOG, UNIQUE_CONSTRAINT_SCHEMA, 
    //  UNIQUE_CONSTRAINT_NAME, MATCH_OPTION, UPDATE_RULE, DELETE_RULE, C_TABLE, U_TABLE
    // Extended version with joins to bring Table names for both constraints
    // Overriding default to get rid of Schema comparisons
    public override DbTable GetReferentialConstraintsExt() {
      var sql = @"
        SELECT rc.*, tc1.TABLE_NAME as C_TABLE, tc2.TABLE_NAME AS U_TABLE 
        FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
          INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc1 
            ON (tc1.CONSTRAINT_NAME = rc.CONSTRAINT_NAME)
          INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc2 
            ON (tc2.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME)";
      return ExecuteSelect(sql);
    }

    public override void OnColumnLoaded(DbColumnInfo column, DbRow columnRow) {
      //check identity flag; SQL CE provides an extra column in information_schema.columns view for identity flag
      var identityFlag = columnRow.GetAsInt("autoinc_increment");
      if(identityFlag == 1)
        column.Flags |= DbColumnFlags.Identity | DbColumnFlags.NoUpdate | DbColumnFlags.NoInsert;
    }



  }
}
