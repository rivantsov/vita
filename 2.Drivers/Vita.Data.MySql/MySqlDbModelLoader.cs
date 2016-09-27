using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Common;

namespace Vita.Data.MySql {
  class MySqlDbModelLoader : DbModelLoader {
    public MySqlDbModelLoader(DbSettings settings, MemoryLog log) : base(settings, log) {
      base.TableTypeTag = "BASE TABLE";
      base.RoutineTypeTag = "PROCEDURE";
    }

    //INFORMATION_SCHEMA does not have a view for indexes, so we have to do it through MSSQL special objects
    public override DbTable GetIndexes() {
      const string getIndexesTemplate = @"
SELECT DISTINCT TABLE_SCHEMA, TABLE_NAME, INDEX_NAME, (0=1) AS PRIMARY_KEY, '' AS CLUSTERED, 
  (-NON_UNIQUE + 1) AS ""UNIQUE"", 0 AS DISABLED, '' as FILTER_CONDITION  
  from information_schema.STATISTICS
where
  INDEX_NAME != 'Primary' AND {0}
ORDER BY TABLE_SCHEMA, TABLE_NAME, INDEX_NAME
";
      var filter = GetSchemaFilter("TABLE_SCHEMA"); 
      var sql = string.Format(getIndexesTemplate, filter);
      return ExecuteSelect(sql);
    }

    public override DbTable GetIndexColumns() {
      const string getIndexColumnsTemplate = @"
SELECT TABLE_SCHEMA, TABLE_NAME, INDEX_NAME, COLUMN_NAME, 
  SEQ_IN_INDEX AS COLUMN_ORDINAL_POSITION, 0 AS IS_DESCENDING
  FROM information_schema.STATISTICS
where
  INDEX_NAME != 'Primary' AND {0}
ORDER BY TABLE_SCHEMA, TABLE_NAME, INDEX_NAME, COLUMN_ORDINAL_POSITION
";
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sql = string.Format(getIndexColumnsTemplate, filter);
      return ExecuteSelect(sql);
    }

    public override DbTypeInfo GetDbTypeInfo(DbRow columnRow) {
      // For 'unsigned' types, Data_type column does not show this attribute, but Column_type does. 
      // We search matching by data_type column (and we register names with 'unsigned' included, like 'int unsigned'). 
      // So we just append the unsigned to data_type value, so lookup works. 
      var columnType = columnRow.GetAsString("COLUMN_TYPE").ToLowerInvariant();
      if(columnType.EndsWith(" unsigned")) {
        columnRow["DATA_TYPE"] += " unsigned";
      }
      //auto-set memo
      var dbType = base.GetDbTypeInfo(columnRow);
      if(dbType != null && dbType.VendorDbType.ClrTypes.Contains(typeof(string)) && dbType.Size > 100 * 1000)
        dbType.Size = -1;
      return dbType; 
    }

    public override void OnModelLoaded() {
      base.OnModelLoaded();
      LoadIdentityColumnsInfo();
      //FixGuidColumnsTypeDefs();
    }

    private void FixGuidColumnsTypeDefs() {
      var guidTypeInfo = Driver.TypeRegistry.FindVendorDbTypeInfo(typeof(Guid), false);
      foreach(var table in Model.Tables) {
        foreach(var col in table.Columns) {
          if(col.TypeInfo.SqlTypeSpec == "binary(16)") {
            col.TypeInfo.VendorDbType = guidTypeInfo; 
          }
        }
      }
    }
    private void LoadIdentityColumnsInfo() {
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sql = string.Format(@"
SELECT table_schema, table_name, column_name
  FROM INFORMATION_SCHEMA.Columns 
  WHERE EXTRA = 'auto_increment' AND {0};", filter);
      var data = ExecuteSelect(sql);
      foreach(DbRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if(!base.IncludeSchema(schema))
          continue;
        var tableName = row.GetAsString("TABLE_NAME");
        var colName = row.GetAsString("COLUMN_NAME");
        var table = Model.GetTable(schema, tableName);
        if(table == null) continue;
        var colInfo = table.Columns.FirstOrDefault(c => c.ColumnName == colName);
        if(colInfo != null)
          colInfo.Flags |= DbColumnFlags.Identity | DbColumnFlags.NoUpdate | DbColumnFlags.NoInsert;
      }
    }

    // In MySql constraint name is not globally unique, it is unique only in scope of the table. 
    // We have to add matching by table names in joins
    public override DbTable GetReferentialConstraintsExt() {
      var filter = GetSchemaFilter("tc1.CONSTRAINT_SCHEMA");
      var sql = string.Format(@"
SELECT rc.*, tc1.TABLE_NAME as C_TABLE, tc2.TABLE_NAME AS U_TABLE 
  FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
    INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc1 
    ON tc1.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA AND 
       tc1.TABLE_NAME = rc.TABLE_NAME AND 
       tc1.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
    INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc2 
    ON tc2.CONSTRAINT_SCHEMA = rc.UNIQUE_CONSTRAINT_SCHEMA AND 
       tc2.TABLE_NAME = rc.REFERENCED_TABLE_NAME AND 
       tc2.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME
  WHERE {0};
", filter);
      return ExecuteSelect(sql);
    }

    protected override void LoadViews() {
      base.LoadViews();
      foreach (var t in Model.Tables) {
        if (t.Kind == EntityKind.View && t.ViewHash == null)
          LoadViewHashFromFormFile(t); 
      }
    }

    // To detect changes in views and routines, we hash sql text and add the hash in comment inside the text. 
    // This works OK in most cases, for stored procs and views. However, MySql (and Postgres) do not save original view definition 
    // MySql removes all comments from view definition. So the only way to retrieve the original source and hash value is to load 
    // it from form file. 
    private void LoadViewHashFromFormFile(DbTableInfo view) {
      const string SqlTemplate =
        "SELECT LOAD_FILE(CONCAT(IFNULL(@@GLOBAL.datadir, CONCAT(@@GLOBAL.basedir, 'data/')), '{0}/{1}.frm')) AS ViewDef;";
      try {
        var sql = string.Format(SqlTemplate, view.Schema, view.TableName);
        var dt = ExecuteSelect(sql);
        if (dt.Rows.Count < 1)
          return;
        // LOAD_FILE should return content as string, but it doesn't, sometimes at least (bug, admitted by MySql team, still not fixed for years)
        // It might return byte array (as it happens on my machine - RI)
        var value = dt.Rows[0]["ViewDef"];
        string viewDef;
        if (value == null)
          return;
        if (value is string)
          viewDef = (string)value;
        else if (value.GetType() == typeof(byte[])) {
          viewDef = Encoding.Default.GetString((byte[])value);
        } else
          return;
        var hashIndex = viewDef.IndexOf(SqlSourceHasher.HashPrefix);
        if (hashIndex < 0)
          return;
        var start = hashIndex + SqlSourceHasher.HashPrefix.Length;
        var starIndex = viewDef.IndexOf('*', start);
        if (starIndex < 0)
          return;
        var hash = viewDef.Substring(start, starIndex - start);
        view.ViewHash = hash; 
      } catch (Exception ex) {
        //Too many things can go wrong, do not break process, just log warning 
        Log.Info("Failed to load view hash for view {0}.{1}, error: {2}.", view.Schema, view.TableName, ex.Message);
      }
    }
  }
}
