using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Data.SQLite {
  public class SQLiteDbModelLoader : DbModelLoader {

    public SQLiteDbModelLoader(DbSettings settings, IActivationLog log) : base(settings, log) {
    }

    public override DbModel LoadModel() {
      Model = new DbModel(Settings.ModelConfig);
      //tables/views, columns
      var tblTables = ExecuteSelect("select type, tbl_name, sql from sqlite_master where type='table' OR type='view';");
      foreach(var tblRow in tblTables.Rows) {
        var tname = tblRow.GetAsString("tbl_name");
        var isView = tblRow.GetAsString("type") == "view";
        var objType = isView ? DbObjectType.View : DbObjectType.Table;
        var tableSql = tblRow.GetAsString("sql");
        var tbl = new DbTableInfo(this.Model, string.Empty, tname, null, objType);
        if (isView) {
          //do not load columns, it will fail 
          tbl.ViewSql = tableSql;
          continue;
        }

        //table columns
        // We detect PKs from the list of indexes. But, for tables with auto-inc columns there's no index
        // However col is marked as PK in table_info listing. So we collect these columns and use them later
        // if PK has not been set
        var pkMarkedCols = new List<DbColumnInfo>();
        var tblCols = ExecuteSelect("PRAGMA table_info('{0}');", tname);
        foreach(var colRow in tblCols.Rows) {
          var colName = colRow.GetAsString("name");
          var type = colRow.GetAsString("type");
          var notNull = colRow.GetAsInt("notnull");
          var dftValue = colRow.GetAsString("dflt_value");
          var typeInfo = GetSqliteTypeInfo(type, nullable: notNull == 0, dft: dftValue);
          if (typeInfo == null) {
            LogError(
              "Failed to find TypeInfo for SQL data type [{0}]. Table(view) {1}, column {2}.", type, tname, colName);
            continue;
          }
          var colInfo = new DbColumnInfo(tbl, colName, typeInfo);
          // check PK flag, save the column if the flag is set
          bool isPk = colRow.GetAsInt("pk") == 1;
          if(isPk)
            pkMarkedCols.Add(colInfo); 
        }// foreach colRow

        // indexes, PKs
        var tblIndexes = ExecuteSelect("PRAGMA index_list('{0}')", tname);
        foreach (var indRow in tblIndexes.Rows) {
          var indName = indRow.GetAsString("name");
          var origin = indRow.GetAsString("origin");
          var unique = indRow.GetAsInt("unique");
          var keyType = KeyType.Index;
          if(origin == "pk")
            keyType = KeyType.PrimaryKey;
          else if(unique == 1)
            keyType |= KeyType.Unique;
          var indCols = ExecuteSelect("PRAGMA index_info('{0}')", indName);
          var indKey = new DbKeyInfo(indName, tbl, keyType);
          foreach(var colRow in indCols.Rows) {
            var colName = colRow.GetAsString("name");
            var col = tbl.Columns.FindByName(colName);
            Util.Check(col != null, "Building index {0}, table {1}: column {2} not found.", indName, tname, colName);
            indKey.KeyColumns.Add(new DbKeyColumnInfo(col));
          }//foreach colRow
          if(keyType.IsSet(KeyType.PrimaryKey))
            tbl.PrimaryKey = indKey; 
        }//foreach indRow
        // check PK; if not detected, it is identity (auto-inc) col (no pk index in this case)
        if (tbl.Kind == EntityKind.Table && tbl.PrimaryKey == null) {
          Util.Check(pkMarkedCols.Count > 0, "Primary key not found on table {0}.", tname);
          CreateAutoIncPrimaryKey(tbl, pkMarkedCols);
        }
      }// foreach tblRow

      // FKs - we need to do it in another loop, after all cols are created. 
      foreach(var tbl in Model.Tables) {
        if(tbl.Kind == EntityKind.View)
          continue;
        var tname = tbl.TableName;
        var tblFKs = ExecuteSelect("PRAGMA foreign_key_list('{0}')", tname);
        var lastKeyId = -1;
        DbRefConstraintInfo constr = null; 
        foreach(var fkColRow in tblFKs.Rows) {
          var fromColName = fkColRow.GetAsString("from");
          var toTableName = fkColRow.GetAsString("table");
          var toColName = fkColRow.GetAsString("to");
          var fromCol = tbl.Columns.FindByName(fromColName);
          Util.Check(fromCol != null, "Loading FKs, table {0}, error: column {1} not found.", tname, fromColName);
          var toTable = FindTable(toTableName);
          Util.Check(toTable != null, "Loading FKs, table {0}, error: target table {1} not found.", tname, toTableName);
          var toCol = toTable.Columns.FindByName(toColName);
          // if keyId is the same as previous one, then col belongs to the same (composite) key
          var keyId = fkColRow.GetAsInt("id");
          if(keyId != lastKeyId) {
            bool cascadeDelete = fkColRow.GetAsString("on_delete") == "CASCADE";
            var keyFrom = new DbKeyInfo("FK_" + tname + "_" + fromColName, tbl, KeyType.ForeignKey);
            constr = new DbRefConstraintInfo(this.Model, keyFrom, toTable.PrimaryKey, cascadeDelete);
            lastKeyId = keyId; 
          }
          constr.FromKey.KeyColumns.Add(new DbKeyColumnInfo(fromCol));          
        }//foreach fkColRow
      }//foreach tbl
      return Model; 
    }//method

    private DbColumnTypeInfo GetSqliteTypeInfo(string type, bool nullable, string dft) {
      var typeDef = Driver.TypeRegistry.FindDbTypeDef(type, false);
      if(typeDef == null)
        return null; 
      var typeInfo = new DbColumnTypeInfo(typeDef, type, nullable, 0, 0, 0, dft);
      return typeInfo; 
    }

    // Model.GetTable searches by full name, and full name is quoted (double quotes) in the model.
    private DbTableInfo FindTable(string name) {
      var qname = "\"" + name + "\"";
      return Model.GetTable(qname);
    }

    // SQLite docs do NOT recommend using AUTOINCREMENT explicitly; for any new row an uninitialized PK column
    // will be auto set to RowID, so it is Auto-inc automatically
    // http://sqlite.org/autoinc.html
    private void CreateAutoIncPrimaryKey(DbTableInfo table, IList<DbColumnInfo> columns) {
      table.PrimaryKey = new DbKeyInfo("PK_" + table.TableName, table, KeyType.PrimaryKey);
      foreach(var col in columns) {
        col.Flags |= DbColumnFlags.PrimaryKey | DbColumnFlags.Identity;
        table.PrimaryKey.KeyColumns.Add(new DbKeyColumnInfo(col));
      } //foreach
    } //method

    // Used by DbInfo module
    public override bool TableExists(string schema, string tableName) {
      // If AppendSchema flag is set, then tableName already includes schema
      var sql = string.Format("select name from sqlite_master where type='table' AND name = '{0}'", tableName);
      var tblRes = ExecuteSelect(sql);
      return tblRes.Rows.Count > 0; 
    }

    protected override string NormalizeViewScript(string script) {
      if(string.IsNullOrEmpty(script))
        return script;
      script = script.Trim();
      // SQLite does not store CREATE VIEW line, just the body; so let's strip off this line from newView
      if(script.StartsWith("CREATE"))
        script = StripFirstLine(script);
      script = script.Trim(_viewTrimChars);
      return script;
    }
    static char[] _viewTrimChars = new char[] { ' ', '\r', '\n', ';' }; //strip ending ;

    protected static string StripFirstLine(string newV) {
      newV = newV.Substring(newV.IndexOf(Environment.NewLine) + Environment.NewLine.Length);
      return newV.Trim(_viewTrimChars);

    }

  }//class

  //* SQLite Error codes
  //#define SQLITE_ERROR        1   /* SQL error or missing database */
  //#define SQLITE_INTERNAL     2   /* Internal logic error in SQLite */
  //#define SQLITE_PERM         3   /* Access permission denied */
  //#define SQLITE_ABORT        4   /* Callback routine requested an abort */
  //#define SQLITE_BUSY         5   /* The database file is locked */
  //#define SQLITE_LOCKED       6   /* A table in the database is locked */
  //#define SQLITE_NOMEM        7   /* A malloc() failed */
  //#define SQLITE_READONLY     8   /* Attempt to write a readonly database */
  //#define SQLITE_INTERRUPT    9   /* Operation terminated by sqlite3_interrupt()*/
  //#define SQLITE_IOERR       10   /* Some kind of disk I/O error occurred */
  //#define SQLITE_CORRUPT     11   /* The database disk image is malformed */
  //#define SQLITE_NOTFOUND    12   /* Unknown opcode in sqlite3_file_control() */
  //#define SQLITE_FULL        13   /* Insertion failed because database is full */
  //#define SQLITE_CANTOPEN    14   /* Unable to open the database file */
  //#define SQLITE_PROTOCOL    15   /* Database lock protocol error */
  //#define SQLITE_EMPTY       16   /* Database is empty */
  //#define SQLITE_SCHEMA      17   /* The database schema changed */
  //#define SQLITE_TOOBIG      18   /* String or BLOB exceeds size limit */
  //#define SQLITE_CONSTRAINT  19   /* Abort due to constraint violation */
  //#define SQLITE_MISMATCH    20   /* Data type mismatch */
  //#define SQLITE_MISUSE      21   /* Library used incorrectly */
  //#define SQLITE_NOLFS       22   /* Uses OS features not supported on host */
  //#define SQLITE_AUTH        23   /* Authorization denied */
  //#define SQLITE_FORMAT      24   /* Auxiliary database format error */
  //#define SQLITE_RANGE       25   /* 2nd parameter to sqlite3_bind out of range */
  //#define SQLITE_NOTADB      26   /* File opened that is not a database file */
  //#define SQLITE_NOTICE      27   /* Notifications from sqlite3_log() */
  //#define SQLITE_WARNING     28   /* Warnings from sqlite3_log() */
  //#define SQLITE_ROW         100  /* sqlite3_step() has another row ready */
  //#define SQLITE_DONE        101  /* sqlite3_step() has finished executing */   

}
