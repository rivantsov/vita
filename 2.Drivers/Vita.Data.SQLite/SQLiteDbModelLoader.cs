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

    public override DbTable GetDatabases() {
      return null; 
    }

    public override DbTable GetSchemas() {
      return null; 
    }

    //Columns: TABLE_CATALOG	TABLE_SCHEMA	TABLE_NAME	TABLE_TYPE  
    public override DbTable GetTables() {
      return GetSchemaCollection("Tables"); 
    }

    public override bool TableExists(string schema, string tableName) {
      var tables = GetTables();
      foreach (DbRow r in tables.Rows) {
        if (r.GetAsString("TABLE_NAME") == tableName)
          return true; 
      }
      return false; 
    }
    //Columns: TABLE_CATALOG	TABLE_SCHEMA	TABLE_NAME VIEW_DEFINITION CHECK_OPTION IS_UPDATABLE  
    public override DbTable GetViews() {
      var views = GetSchemaCollection("Views");
      return views; 
    }

    public override DbTable GetColumns() {
      var dtCol = GetSchemaCollection("Columns");
      // IS_NULLABLE column is bool here, but standard expected arrangement is string (yes/no)
      // so let's reformat the DbTable
      dtCol.FindColumn("IS_NULLABLE").Name = "IS_NULLABLE_BOOL";
      dtCol.AddColumn("IS_NULLABLE", typeof(string));
      foreach(DbRow row in dtCol.Rows) {
        var isNull =  (bool) row["IS_NULLABLE_BOOL"];
        row["IS_NULLABLE"] = isNull ? "YES" : "NO";
        // for view columns data type string is empty; set it to blob
        var dataType = row.GetAsString("DATA_TYPE");
        if (string.IsNullOrWhiteSpace(dataType))
          row["DATA_TYPE"] = "blob"; 
      }
      return dtCol;
    }

    public override DbTable GetTableConstraints() {
      var dtAll = GetSchemaCollection("ForeignKeys"); //this gives us only foreign keys
      // We need to add PKs; Each PK in SQLite is 'supported' by an index named 'sqlite_autoindex_*'
      // We scan index columns to pick up such names and add PK rows to dtAll.
      //Add PKs by scanning index columns and finding special-named indexes (starting with sqlite_autoindex)
      var dtIndexes = GetIndexColumns();
      var tNames = new StringSet(); //track tables to prevent duplicates
      foreach(DbRow row in dtIndexes.Rows) {
        var ixName = row.GetAsString("INDEX_NAME");
        if(!IsPrimaryKeyIndex(ixName))
          continue;
        var tblName = row.GetAsString("TABLE_NAME");
        if (tNames.Contains(tblName)) continue; //don't add duplicates
        tNames.Add(tblName); 
        //it is auto-index for PK, create a row for the index
        var pkRow = dtAll.AddRow();
        pkRow["TABLE_NAME"] = tblName;
        pkRow["CONSTRAINT_NAME"] = row.GetAsString("INDEX_NAME");
        pkRow["CONSTRAINT_TYPE"] = "PRIMARY KEY";
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
    public override DbTable GetTableConstraintColumns() {
      //We manually construct result table by merging information from 2 sources - for PKs and FKs, separately
      var dtCols = new DbTable();
      dtCols.AddColumn("TABLE_NAME", typeof(string));
      dtCols.AddColumn("COLUMN_NAME", typeof(string));
      dtCols.AddColumn("CONSTRAINT_NAME", typeof(string));
      dtCols.AddColumn("ORDINAL_POSITION", typeof(int));
      dtCols.AddColumn("TABLE_CATALOG", typeof(string));
      dtCols.AddColumn("TABLE_SCHEMA", typeof(string));
      dtCols.AddColumn("CONSTRAINT_SCHEMA", typeof(string));
      dtCols.AddColumn("CONSTRAINT_CATALOG", typeof(string));

      //Add Primary key columns
      var dtIndexCols = GetIndexColumns();
      foreach(DbRow pkRow in dtIndexCols.Rows) {
        var ixName = pkRow.GetAsString("INDEX_NAME");
        if(IsPrimaryKeyIndex(ixName))
          AddKeyColumnRow(dtCols, ixName, pkRow.GetAsString("TABLE_NAME"), pkRow.GetAsString("COLUMN_NAME"), pkRow.GetAsInt("COLUMN_ORDINAL_POSITION"));
      }

      //Add Foreign key columns
      foreach(var tbl in this.Model.Tables) {
        var sql = string.Format("pragma foreign_key_list([{0}])", tbl.TableName);
        // returns id, seq, table, from, to, on_update, on_delete, match
        var dt = ExecuteSelect(sql);
        foreach(DbRow fkRow in dt.Rows) {
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
    private DbRow AddKeyColumnRow(DbTable table, string constrName, string tableName, string columnName, int ordinalPosition) {
      var row = table.AddRow();
      row["CONSTRAINT_NAME"] = constrName;
      row["TABLE_NAME"] = tableName;
      row["COLUMN_NAME"] = columnName;
      row["ORDINAL_POSITION"] = ordinalPosition;
      return row; 
    }



    public override DbTable GetReferentialConstraintsExt() {
      var dt = GetSchemaCollection("ForeignKeys");
      //adjust column names
      dt.FindColumn("TABLE_NAME").Name = "C_TABLE";
      dt.FindColumn("FKEY_TO_SCHEMA").Name = "UNIQUE_CONSTRAINT_SCHEMA";
      dt.FindColumn("FKEY_TO_TABLE").Name = "U_TABLE";
      dt.FindColumn("FKEY_ON_DELETE").Name = "DELETE_RULE";
      return dt;
    }
    public override DbTable GetIndexes() {
      var dt = GetSchemaCollection("Indexes");
      return dt; 
    }

    // Rename ORDINAL_POSITION to COLUMN_ORDINAL_POSITION and increment the value (it should be 1-based)
    // Change SORT_MODE (ASC/DESC: string) into bool column IS_DESCENDING
    public override DbTable GetIndexColumns() {
      var colOrdPos = "column_ordinal_position";
      var dt = GetSchemaCollection("INDEXCOLUMNS");
      dt.FindColumn("ORDINAL_POSITION").Name = colOrdPos;
      dt.AddColumn("IS_DESCENDING", typeof(int));
      foreach(DbRow row in dt.Rows) {
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
    private DbTable GetSchemaCollection(string collectionName) {
      var conn = new SQLiteConnection(Settings.ConnectionString);
      try {
        conn.Open();
        var coll = conn.GetSchema(collectionName);
        return ToDbTable(coll);
      } finally {
        conn.Close(); 
      }
    }

    private DbTable ToDbTable(System.Data.DataTable table) {
      var tbl = new DbTable();
      foreach(DataColumn col in table.Columns)
        tbl.AddColumn(col.ColumnName, col.DataType);
      foreach(DataRow drow in table.Rows) {
        var row = tbl.AddRow();
        foreach(var c in tbl.Columns)
          row[c.Index] = drow[c.Index];
      }
      return tbl; 
    }
  }//class

}
