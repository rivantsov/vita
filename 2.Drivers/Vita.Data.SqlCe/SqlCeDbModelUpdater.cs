using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Upgrades;

namespace Vita.Data.SqlCe {
  public class SqlCeDbModelUpdater : DbModelUpdater {

    public SqlCeDbModelUpdater(DbSettings settings) : base(settings) { }

    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      change.AddScript(DbScriptType.TableRename, "sp_rename '{0}', '{1}';", oldTable.TableName, newTable.TableName);
    }
    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      //SQL CE has no command to rename columns, so we use add/copy/drop method which creates new column, copies data and removes old one
      base.BuildColumnRenameSqlWithAddDrop(change, oldColumn, newColumn); 
    }
    public override void BuildIndexDropSql(DbObjectChange change, DbKeyInfo key) {
      change.AddScript(DbScriptType.IndexDrop, "DROP INDEX {0}.\"{1}\";", key.Table.FullName, key.Name);
    }
    public override void BuildColumnModifySql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      //SqlCe does not allow any modification of 'ntext'/memo columns
      var dbType = column.TypeInfo.DbType;
      bool isNText = column.TypeInfo.Size < 0 && (dbType == DbType.String || dbType == DbType.Binary);
      if(isNText) {
        change.NotSupported("Modifying ntext column not supported in SqlCE. Column: {0}.{1}", column.Table.TableName, column.ColumnName);
        return;
      }
      base.BuildColumnModifySql(change, column, options);
    }

    protected override string GetColumnSpec(DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      var typeStr = column.TypeInfo.SqlTypeSpec;
      var nullable = options.IsSet(DbScriptOptions.ForceNull) || column.Flags.IsSet(DbColumnFlags.Nullable);
      var nullStr = nullable ? "NULL" : "NOT NULL";
      var idStr = string.Empty;
      bool isNew = column.Peer == null;
      if(isNew && column.Flags.IsSet(DbColumnFlags.Identity)) {
        idStr = "IDENTITY(1,1)";
      }
      string defaultStr = null;
      //Default constraint can be set only on new columns in SQL server
      if(!string.IsNullOrWhiteSpace(column.DefaultExpression) && isNew)
        defaultStr = "DEFAULT " + column.DefaultExpression;
      var spec = string.Format(" \"{0}\" {1} {2} {3} {4}", column.ColumnName, typeStr, idStr, defaultStr, nullStr);
      return spec;
    }


  }//class
}
