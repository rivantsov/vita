using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Data.SQLite {
  public class SQLiteDbModelLoader : DbModelLoader {

    public SQLiteDbModelLoader(DbSettings settings, MemoryLog log) : base(settings, log) {
    }

    public override DataTable GetDatabases() {
      return null; 
    }

    public override DataTable GetSchemas() {
      return null; 
    }

    //Columns: TABLE_CATALOG	TABLE_SCHEMA	TABLE_NAME	TABLE_TYPE  
    public override DataTable GetTables() {
      return GetSchemaCollection("Tables"); 
    }

    public override bool TableExists(string schema, string tableName) {
      var tables = GetTables();
      foreach (DataRow r in tables.Rows) {
        if (r.GetAsString("TABLE_NAME") == tableName)
          return true; 
      }
      return false; 
    }
    //Columns: TABLE_CATALOG	TABLE_SCHEMA	TABLE_NAME VIEW_DEFINITION CHECK_OPTION IS_UPDATABLE  
    public override DataTable GetViews() {
      var views = GetSchemaCollection("Views");
      return views; 
    }

    public override DataTable GetColumns() {
      var dtCol = GetSchemaCollection("Columns");
      // IS_NULLABLE column is bool here, but standard expected arrangement is string (yes/no)
      // so let's reformat the datatable
      dtCol.Columns["IS_NULLABLE"].ColumnName = "IS_NULLABLE_BOOL";
      dtCol.Columns.Add("IS_NULLABLE", typeof(string));
      foreach(DataRow row in dtCol.Rows) {
        var isNull =  (bool) row["IS_NULLABLE_BOOL"];
        row["IS_NULLABLE"] = isNull ? "YES" : "NO";
        // for view columns data type string is empty; set it to blob
        var dataType = row.GetAsString("DATA_TYPE");
        if (string.IsNullOrWhiteSpace(dataType))
          row["DATA_TYPE"] = "blob"; 
      }
      return dtCol;
    }

    public override DataTable GetTableConstraints() {
      var dtAll = GetSchemaCollection("ForeignKeys"); //this gives us only foreign keys
      // We need to add PKs; Each PK in SQLite is 'supported' by an index named 'sqlite_autoindex_*'
      // We scan index columns to pick up such names and add PK rows to dtAll.
      //Add PKs by scanning index columns and finding special-named indexes (starting with sqlite_autoindex)
      var dtIndexes = GetIndexColumns();
      var tNames = new StringSet(); //track tables to prevent duplicates
      foreach(DataRow row in dtIndexes.Rows) {
        var ixName = row.GetAsString("INDEX_NAME");
        if(!IsPrimaryKeyIndex(ixName))
          continue;
        var tblName = row.GetAsString("TABLE_NAME");
        if (tNames.Contains(tblName)) continue; //don't add duplicates
        tNames.Add(tblName); 
        //it is auto-index for PK, create a row for the index
        var pkRow = dtAll.NewRow();
        pkRow["TABLE_NAME"] = tblName;
        pkRow["CONSTRAINT_NAME"] = row.GetAsString("INDEX_NAME");
        pkRow["CONSTRAINT_TYPE"] = "PRIMARY KEY";
        dtAll.Rows.Add(pkRow); 
      }
      return dtAll; 
    }

    // Each PK in SQLite is 'supported' by an index named 'sqlite_autoindex_*'; for identity columns index name is 'sqlite_master_PK_*'
    private bool IsPrimaryKeyIndex(string indexName) {
      //The 
      return indexName.StartsWith("sqlite_autoindex_") || indexName.StartsWith("sqlite_master_PK_");
    }

    // Columns: TABLE_NAME	COLUMN_NAME ORDINAL_POSITION CONSTRAINT_NAME 
    // Empty: TABLE_CATALOG	TABLE_SCHEMA 	CONSTRAINT_CATALOG CONSTRAINT_SCHEMA			
    public override DataTable GetTableConstraintColumns() {
      //We manually construct result table by merging information from 2 sources - for PKs and FKs, separately
      var dtCols = new DataTable();
      dtCols.Columns.Add("TABLE_NAME", typeof(string));
      dtCols.Columns.Add("COLUMN_NAME", typeof(string));
      dtCols.Columns.Add("CONSTRAINT_NAME", typeof(string));
      dtCols.Columns.Add("ORDINAL_POSITION", typeof(int));
      dtCols.Columns.Add("TABLE_CATALOG", typeof(string));
      dtCols.Columns.Add("TABLE_SCHEMA", typeof(string));
      dtCols.Columns.Add("CONSTRAINT_SCHEMA", typeof(string));
      dtCols.Columns.Add("CONSTRAINT_CATALOG", typeof(string));

      //Add Primary key columns
      var dtIndexCols = GetIndexColumns();
      var dtResult = dtIndexCols.Clone();
      foreach(DataRow pkRow in dtIndexCols.Rows) {
        var ixName = pkRow.GetAsString("INDEX_NAME");
        if(IsPrimaryKeyIndex(ixName))
          AddKeyColumnRow(dtCols, ixName, pkRow.GetAsString("TABLE_NAME"), pkRow.GetAsString("COLUMN_NAME"), pkRow.GetAsInt("COLUMN_ORDINAL_POSITION"));
      }

      //Add Foreign key columns
      foreach(var tbl in this.Model.Tables) {
        var sql = string.Format("pragma foreign_key_list([{0}])", tbl.TableName);
        // returns id, seq, table, from, to, on_update, on_delete, match
        var dt = ExecuteSelect(sql);
        foreach(DataRow fkRow in dt.Rows) {
          var fromColName = fkRow.GetAsString("from");
          var fromCol = tbl.Columns.FindByName(fromColName);
          var id = fkRow.GetAsInt("id");
          var keyNamePrefix = "FK_" + tbl.TableName + "_" + id + "_";
          var key = tbl.Keys.FirstOrDefault(k => k.Name.StartsWith(keyNamePrefix));
          if(key != null)
            AddKeyColumnRow(dtCols, key.Name, tbl.TableName, fkRow.GetAsString("from"), fkRow.GetAsInt("seq"));
        }
      }
      return dtCols;
    }

    //helper method
    private DataRow AddKeyColumnRow(DataTable table, string constrName, string tableName, string columnName, int ordinalPosition) {
      var row = table.NewRow();
      row["CONSTRAINT_NAME"] = constrName;
      row["TABLE_NAME"] = tableName;
      row["COLUMN_NAME"] = columnName;
      row["ORDINAL_POSITION"] = ordinalPosition;
      table.Rows.Add(row); 
      return row; 
    }



    public override DataTable GetReferentialConstraintsExt() {
      var dt = GetSchemaCollection("ForeignKeys");
      //adjust column names
      dt.Columns["TABLE_NAME"].ColumnName = "C_TABLE";
      dt.Columns["FKEY_TO_SCHEMA"].ColumnName = "UNIQUE_CONSTRAINT_SCHEMA";
      dt.Columns["FKEY_TO_TABLE"].ColumnName = "U_TABLE";
      dt.Columns["FKEY_ON_DELETE"].ColumnName = "DELETE_RULE";
      return dt;
    }
    public override DataTable GetIndexes() {
      var dt = GetSchemaCollection("Indexes");
      return dt; 
    }

    // Rename ORDINAL_POSITION to COLUMN_ORDINAL_POSITION and increment the value (it should be 1-based)
    // Change SORT_MODE (ASC/DESC: string) into bool column IS_DESCENDING
    public override DataTable GetIndexColumns() {
      var colOrdPos = "column_ordinal_position";
      var dt = GetSchemaCollection("INDEXCOLUMNS");
      dt.Columns["ORDINAL_POSITION"].ColumnName = colOrdPos;
      dt.Columns.Add("IS_DESCENDING", typeof(int));
      foreach(DataRow row in dt.Rows) {
        var sm = row.GetAsString("SORT_MODE");
        row["IS_DESCENDING"] = sm == "ASC" ? 0 : -1;
        //ordinal position is usually 1-based, but SQLite seems to be 0-based; so we increment the value
        row[colOrdPos] = row.GetAsInt(colOrdPos) + 1;
      }
      return dt; 
    }

    // See GetSchema source here: 
    // https://github.com/OpenDataSpace/System.Data.SQLite/blob/master/System.Data.SQLite/SQLiteConnection.cs
    // Note that not all standard collections are supported
    private DataTable GetSchemaCollection(string collectionName) {
      var conn = new SQLiteConnection(Settings.ConnectionString);
      try {
        conn.Open();
        var coll = conn.GetSchema(collectionName);
        return coll;
      } finally {
        conn.Close(); 
      }
    }

  }//class

}
