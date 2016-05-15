using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities.Model;

namespace Vita.Data.MySql {
  public class MySqlDbModelUpdater : DbModelUpdater {

    public MySqlDbModelUpdater(DbSettings settings) : base(settings) { }

    public override void BuildPrimaryKeyAddSql(DbObjectChange change, DbKeyInfo key) {
      // PK for Identity (Auto-increment) columns is created when table/ID columns is created
      if(key.KeyColumns[0].Column.Flags.IsSet(DbColumnFlags.Identity)) {
        change.AddScript(DbScriptType.PrimaryKeyAdd, "-- PrimaryKeyAdd empty action");
        return; 
      }
      
      var fullTableRef = key.Table.FullName;
      var pkFields = key.KeyColumns.GetSqlNameList();
      // PK name is always 'PRIMARY'
      change.AddScript(DbScriptType.PrimaryKeyAdd, "ALTER TABLE {0} ADD CONSTRAINT PRIMARY KEY ({1});", fullTableRef, pkFields);
    }

    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      change.AddScript(DbScriptType.TableRename, "ALTER TABLE {0} RENAME TO {1};", oldTable.FullName, newTable.FullName);
    }

    public override void BuildRefConstraintDropSql(DbObjectChange change, DbRefConstraintInfo dbRefConstraint) {
      var fromKey = dbRefConstraint.FromKey;
      change.AddScript(DbScriptType.RefConstraintDrop, "ALTER TABLE {0} DROP FOREIGN KEY {1};", fromKey.Table.FullName, fromKey.Name);
    }

    public override void BuildTableConstraintDropSql(DbObjectChange change, DbKeyInfo key) {
      if(key.KeyType == Entities.Model.KeyType.PrimaryKey) {
        change.AddScript(DbScriptType.RefConstraintDrop, "ALTER TABLE {0} DROP PRIMARY KEY;", key.Table.FullName);
      }
    }

    public override void BuildColumnModifySql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      var colSpec = GetColumnSpec(column, options);
      var tbl = column.Table;
      var scriptType = options.IsSet(DbScriptOptions.CompleteColumnSetup) ? DbScriptType.ColumnSetupComplete : DbScriptType.ColumnModify;
      change.AddScript(scriptType, "ALTER TABLE {0} MODIFY COLUMN {1};", tbl.FullName, colSpec);
    }

    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      var colSpec = GetColumnSpec(newColumn, DbScriptOptions.None);
      change.AddScript(DbScriptType.ColumnRename, "ALTER TABLE {0} CHANGE COLUMN \"{1}\" {2};", newColumn.Table.FullName, oldColumn.ColumnName, colSpec);
    }


    protected override string GetColumnSpec(DbColumnInfo column, DbScriptOptions options) {
      var typeStr = column.TypeInfo.SqlTypeSpec;
      var nullable = options.IsSet(DbScriptOptions.ForceNull) || column.Flags.IsSet(DbColumnFlags.Nullable);
      var nullStr = nullable ? "NULL" : "NOT NULL";
      var strAutoInc = string.Empty;
      bool isNew = column.Peer == null;
      if(isNew && column.Flags.IsSet(DbColumnFlags.Identity)) {
        // MySql requires that auto-incr column is supported by a key - either a primary key, or an index
        var strKeyType = column.Flags.IsSet(DbColumnFlags.PrimaryKey) ? "PRIMARY KEY" : "KEY";
        strAutoInc = string.Format("AUTO_INCREMENT, {0}(\"{1}\")", strKeyType, column.ColumnName);
      }
      string defaultStr = null;
      //Default constraint can be set only on new columns
      if(!string.IsNullOrWhiteSpace(column.DefaultExpression) && options.IsSet(DbScriptOptions.NewColumn))
        defaultStr = "DEFAULT " + column.DefaultExpression;
      var spec = string.Format(@" ""{0}"" {1} {2} {3}", column.ColumnName, typeStr, nullStr, strAutoInc);
      return spec;
    }

  }//class
}
