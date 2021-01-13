using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities;
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
      
      var tn = key.Table.FullName;
      var pkFields = key.KeyColumns.GetSqlNameList();
      // PK name is always 'PRIMARY'
      change.AddScript(DbScriptType.PrimaryKeyAdd, $"ALTER TABLE {tn} ADD CONSTRAINT PRIMARY KEY ({pkFields});");
    }

    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      change.AddScript(DbScriptType.TableRename, $"ALTER TABLE {oldTable.FullName} RENAME TO {newTable.FullName};");
    }

    public override void BuildRefConstraintDropSql(DbObjectChange change, DbRefConstraintInfo dbRefConstraint) {
      var fromKey = dbRefConstraint.FromKey;
      var kn = QuoteName(fromKey.Name); 
      change.AddScript(DbScriptType.RefConstraintDrop, $"ALTER TABLE {fromKey.Table.FullName} DROP FOREIGN KEY {kn};");
    }

    public override void BuildTableConstraintDropSql(DbObjectChange change, DbKeyInfo key) {
      if(key.KeyType == KeyType.PrimaryKey) {
        change.AddScript(DbScriptType.RefConstraintDrop, $"ALTER TABLE {key.Table.FullName} DROP PRIMARY KEY;");
      } else
        base.BuildTableConstraintDropSql(change, key); 
    }

    public override void BuildColumnModifySql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      var colSpec = GetColumnSpec(column, options);
      var tbl = column.Table;
      var scriptType = options.IsSet(DbScriptOptions.CompleteColumnSetup) ? DbScriptType.ColumnSetupComplete : DbScriptType.ColumnModify;
      change.AddScript(scriptType, $"ALTER TABLE {tbl.FullName} MODIFY COLUMN {colSpec};");
    }

    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      var colSpec = GetColumnSpec(newColumn, DbScriptOptions.None);
      var tn = newColumn.Table.FullName;
      change.AddScript(DbScriptType.ColumnRename, $"ALTER TABLE {tn} CHANGE COLUMN {oldColumn.ColumnNameQuoted} {colSpec};");
    }

    protected override string GetColumnSpec(DbColumnInfo column, DbScriptOptions options) {
      var typeStr = column.TypeInfo.DbTypeSpec;
      var nullable = options.IsSet(DbScriptOptions.ForceNull) || column.Flags.IsSet(DbColumnFlags.Nullable);
      var nullStr = nullable ? "NULL" : "NOT NULL";
      var strAutoInc = string.Empty;
      bool isNew = options.IsSet(DbScriptOptions.NewColumn);
      if(isNew && column.Flags.IsSet(DbColumnFlags.Identity)) {
        // MySql requires that auto-incr column is supported by a key - either a primary key, or an index
        var strKeyType = column.Flags.IsSet(DbColumnFlags.PrimaryKey) ? "PRIMARY KEY" : "KEY";
        strAutoInc = $"AUTO_INCREMENT, {strKeyType}({column.ColumnNameQuoted})";
      }
      string defaultStr = null;
      //Default constraint can be set only on new columns
      if(!string.IsNullOrWhiteSpace(column.DefaultExpression) && options.IsSet(DbScriptOptions.NewColumn))
        defaultStr = "DEFAULT " + column.DefaultExpression;
      var spec = $" {column.ColumnNameQuoted} {typeStr} {nullStr} {strAutoInc}"; 
      return spec;
    }

  }//class
}
