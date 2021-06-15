using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oracle.ManagedDataAccess.Client;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities.Model;

namespace Vita.Data.Oracle {

  public class OracleDbModelUpdater : DbModelUpdater {
    string _defaultTableSpace;
    string _defaultTempTableSpace;

    public OracleDbModelUpdater(DbSettings settings) : base(settings) {
      _defaultTableSpace = settings.GetCustomSetting(OracleDbDriver.SettingsKeyDefaultTableSpace, "USERS");
      _defaultTempTableSpace = settings.GetCustomSetting(OracleDbDriver.SettingsKeyDefaultTableSpace, "TEMP");
    }
    public override void BuildScripts(DbUpgradeInfo upgradeInfo) {
      base.BuildScripts(upgradeInfo);
      // cut-off ending semicolon
      foreach(var scr in upgradeInfo.AllScripts) {
        scr.Sql = scr.Sql.TrimEnd(' ', '\r', '\n', ';');
      }
    }

    public override void BuildScripts(DbObjectChange change) {
      if (change.DbObject.ObjectType == DbObjectType.Schema && change.NewObject != null) {
        // we are adding schema, use custom oracle method below - it requires full object to get tablespace
        BuildSchemaAddSql(change, (DbSchemaInfo) change.NewObject);
      } else 
      base.BuildScripts(change);
    }

    public void BuildSchemaAddSql(DbObjectChange change, DbSchemaInfo schemaObj) {
      change.AddScript(DbScriptType.ScriptInit, "ALTER SESSION SET \"_ORACLE_SCRIPT\" = true;");
      var qsch = QuoteName(schemaObj.Schema);
      var ts = schemaObj.Area?.OracleTableSpace ?? _defaultTableSpace;
      var tempTs = _defaultTempTableSpace;
      var scriptNewUser =$@"
CREATE USER {qsch} identified by password 
  DEFAULT TABLESPACE {ts}
  TEMPORARY TABLESPACE {tempTs}
";
      change.AddScript(DbScriptType.SchemaAdd, scriptNewUser);
      change.AddScript(DbScriptType.SchemaAdd, $"GRANT CONNECT, CREATE TABLE TO {qsch}");
      change.AddScript(DbScriptType.SchemaAdd, $"ALTER USER {qsch} quota unlimited on {ts}");
    }

    public override void BuildSchemaDropSql(DbObjectChange change, string schema) {
      change.AddScript(DbScriptType.ScriptInit, "ALTER SESSION SET \"_ORACLE_SCRIPT\" = true;");
      var qsch = QuoteName(schema);
      change.AddScript(DbScriptType.SchemaDrop, $@"
DROP USER {qsch} CASCADE;
");
    }

    public override void BuildTableAddSql(DbObjectChange change, DbTableInfo table) {
      var realColumns = base.GetTableColumns(table); 
      var colSpecList = realColumns.Select(c => GetColumnSpec(c, DbScriptOptions.NewColumn));
      var columnSpecs = string.Join(", " + Environment.NewLine, colSpecList);
      var script =
$@"CREATE TABLE {table.FullName} (
{columnSpecs}
) ";
      var ts = table.Entity.Area.OracleTableSpace;
      if(!string.IsNullOrEmpty(ts))
        script += " TABLESPACE " + ts;
      change.AddScript(DbScriptType.TableAdd, script);
    }

    public override void BuildIndexAddSql(DbObjectChange change, DbKeyInfo key) {
      // custom version, cannot use default script builder - full index name should include schema name;
      // plus Oracle does not have includes and filters, so script is much simpler
      var driver = this.Settings.Driver;
      var unique = key.KeyType.IsSet(KeyType.Unique) ? "UNIQUE" : string.Empty;
      string indexFields = key.KeyColumns.GetSqlNameList();
      var qKeyName = QuoteName(key.Name);
      var qSch = QuoteName(key.Schema); 
      var script = $@"
CREATE {unique} INDEX {qSch}.{qKeyName}  
  ON {key.Table.FullName} ( {indexFields} )
";
      change.AddScript(DbScriptType.IndexAdd, script);
    }

    public override void BuildIndexDropSql(DbObjectChange change, DbKeyInfo key) {
      if(key.KeyType.IsSet(KeyType.PrimaryKey))
        return; // PK indexes cannot be dropped
      var qn = QuoteName(key.Name);
      var sch = QuoteName(key.Schema); 
      change.AddScript(DbScriptType.IndexDrop, $"DROP INDEX {sch}.{qn}");
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
      bool isComputed = column.ComputedKind != DbComputedKindExt.None;
      if (isComputed)
        return GetComputedColumnSpec(column, options);

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

    private string GetComputedColumnSpec(DbColumnInfo column, DbScriptOptions options) {
      var typeStr = column.TypeInfo.DbTypeSpec;
      var virtStored = column.ComputedKind == DbComputedKindExt.Column ? "VIRTUAL" : "STORED";
      var spec =
        $"{column.ColumnNameQuoted} {typeStr} GENERATED ALWAYS AS ({column.ComputedAsExpression}) {virtStored}";
      return spec;
    }



    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      var tn = newColumn.Table.FullName;
      change.AddScript(DbScriptType.ColumnRename, $"ALTER TABLE {tn} RENAME COLUMN {oldColumn.ColumnNameQuoted} TO {newColumn.ColumnNameQuoted}");
    }

    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      // Note: new name should be without schema, otherwise error 
      var newName = QuoteName(newTable.TableName);
      change.AddScript(DbScriptType.TableRename, $"ALTER TABLE {oldTable.FullName} RENAME TO {newName};");
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

    public override void BuildSequenceDropSql(DbObjectChange change, DbSequenceInfo sequence) {
      if(sequence.Name.StartsWith("ISEQ$$"))
        return; // this is sequence created automatically to support identity column - these should not be deleted explicitly
      base.BuildSequenceDropSql(change, sequence);
    }
  }
}
