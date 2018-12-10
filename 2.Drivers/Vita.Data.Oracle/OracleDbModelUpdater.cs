using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities.Model;

namespace Vita.Data.Oracle {
  public class OracleDbModelUpdater : DbModelUpdater {

    public OracleDbModelUpdater(DbSettings settings) : base(settings) {

    }
    public override void BuildScripts(DbUpgradeInfo upgradeInfo) {
      base.BuildScripts(upgradeInfo);
      // cut-off ending semicolon
      foreach(var scr in upgradeInfo.AllScripts) {
        scr.Sql = scr.Sql.TrimEnd(' ', '\r', '\n', ';');
      }
    }

    public override void BuildTableAddSql(DbObjectChange change, DbTableInfo table) {
      base.BuildTableAddSql(change, table);
      var ts = table.Entity.Area.OracleTableSpace; 
      if (!string.IsNullOrEmpty(ts)) {
        var script = change.Scripts.Last();
        script.Sql = script.Sql.TrimEnd(';', ' ') + " TABLESPACE " + ts; 
      }
    }

    public override void BuildIndexDropSql(DbObjectChange change, DbKeyInfo key) {
      var qn = QuoteName(key.Name);
      change.AddScript(DbScriptType.IndexDrop, $"DROP INDEX {qn}");
    }

    public override void BuildColumnModifySql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      var scriptType = options.IsSet(DbScriptOptions.CompleteColumnSetup) ? DbScriptType.ColumnSetupComplete : DbScriptType.ColumnModify;
      if(ShouldResetNullsToDefault(column)) {
        BuildColumnSetDefaultValuesSql(change, column);
        scriptType = DbScriptType.ColumnSetupComplete;
      }
      // check if Nullability changed
      var oldCol = (DbColumnInfo)change.OldObject;
      // CompleteColumnSetup is indicator we are changing nullability on new columns
      var nullChange = options.IsSet(DbScriptOptions.CompleteColumnSetup) ||
                           column.Flags.IsSet(DbColumnFlags.Nullable) != oldCol.Flags.IsSet(DbColumnFlags.Nullable);
      if (nullChange)
        options |= DbScriptOptions.NullabilityChange; 
      var colSpec = GetColumnSpec(column, options);
      change.AddScript(scriptType, $"ALTER TABLE {column.Table.FullName} MODIFY {colSpec};");
    }

    // Oracle when changing column, expects Null/NotNull only if it is changing (otherwise, if it is the same, throws stupid error)
    protected override string GetColumnSpec(DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      var typeStr = column.TypeInfo.DbTypeSpec;
      var idStr = string.Empty;
      bool isNew = options.IsSet(DbScriptOptions.NewColumn);
      if(isNew && column.Flags.IsSet(DbColumnFlags.Identity))
        idStr = "GENERATED AS IDENTITY";
      string defaultStr = null;
      //Default constraint can be set only on new columns in SQL server
      if(!string.IsNullOrWhiteSpace(column.DefaultExpression) && isNew)
        defaultStr = "DEFAULT " + column.DefaultExpression;
      // Null/Not Null
      string nullStr = string.Empty; 
      if (options.IsSet(DbScriptOptions.NewColumn | DbScriptOptions.NullabilityChange)) {
        var nullable = options.IsSet(DbScriptOptions.ForceNull) || column.Flags.IsSet(DbColumnFlags.Nullable);
        nullStr = nullable ? "NULL" : "NOT NULL";
      }
      var spec = $" {column.ColumnNameQuoted} {typeStr} {idStr} {defaultStr} {nullStr}";
      return spec;
    }

    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      var tn = newColumn.Table.FullName;
      change.AddScript(DbScriptType.ColumnRename, $"ALTER TABLE {tn} RENAME COLUMN {oldColumn.ColumnNameQuoted} TO {newColumn.ColumnNameQuoted}");
    }

    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      base.BuildTableRenameSql(change, oldTable, newTable);
    }

    public override void BuildViewAddSql(DbObjectChange change, DbTableInfo view) {
      // For indexed views add 'MATERIALIZED' attribute
      var matz = view.IsMaterializedView ? "MATERIALIZED" : string.Empty;
      var script =
$@"CREATE {matz} VIEW {view.FullName}  AS 
  {view.ViewSql}
";
      change.AddScript(DbScriptType.ViewAdd, script);
    }
  }
}
