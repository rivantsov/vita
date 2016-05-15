using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities.Model;

namespace Vita.Data.MsSql {
  public partial class MsSqlDbModelUpdater : DbModelUpdater {

    public MsSqlDbModelUpdater(DbSettings settings) : base(settings) { }

    public override void BuildScripts(DbUpgradeInfo upgradeInfo) {
      base.BuildScripts(upgradeInfo);
    }

    public override void BuildDatabaseAddSql(DbObjectChange change, string name) {
      change.AddScript(DbScriptType.DatabaseAdd, "CREATE DATABASE \"{0}\"", name);
    }

    public override void BuildSchemaAddSql(DbObjectChange change, string schema) {
      change.AddScript(DbScriptType.SchemaAdd, "CREATE SCHEMA \"{0}\";", schema);
    }//method

    public override void BuildTableAddSql(DbObjectChange change, DbTableInfo table) {
      const string SqlTemplate = @"CREATE TABLE {0} (" + "\r\n {1} \r\n); ";
      var specs = table.Columns.Select(c => GetColumnSpec(c));
      var columnSpecs = string.Join("," + Environment.NewLine, specs);
      change.AddScript(DbScriptType.TableAdd, SqlTemplate, table.FullName, columnSpecs);
    }//method

    public override void BuildStoredProcAddSql(DbObjectChange change, DbCommandInfo command) {
      base.BuildStoredProcAddSql(change, command);
      const string GrantTemplate = "GRANT EXECUTE ON {0} TO [{1}];";
      if (command.EntityCommand.Kind.IsSelect()) {
        if(!string.IsNullOrWhiteSpace(Settings.GrantExecReadToRole))
          change.AddScript(DbScriptType.Grant, GrantTemplate, command.FullCommandName, Settings.GrantExecReadToRole);
      } else {
        if (!string.IsNullOrWhiteSpace(Settings.GrantExecWriteToRole))
          change.AddScript(DbScriptType.Grant, GrantTemplate, command.FullCommandName, Settings.GrantExecWriteToRole);
      }
    }

    public override void BuildViewAddSql(DbObjectChange change, DbTableInfo view) {
      const string sqlTemplate =
@"CREATE VIEW {0} {1}  AS 
  {2}"; //notice - no ';' at the end, SQL must have it already
      // For materialized views add 'With SCHEMABINDING' attribute
      var attrs = view.IsMaterializedView ? "WITH SCHEMABINDING" : string.Empty; 
      change.AddScript(DbScriptType.ViewAdd, sqlTemplate, view.FullName, attrs, view.ViewSql);
      const string GrantSelectTemplate = "GRANT SELECT ON {0} TO [{1}];";
      if (!string.IsNullOrWhiteSpace(Settings.GrantExecReadToRole))
        change.AddScript(DbScriptType.Grant, GrantSelectTemplate, view.FullName, Settings.GrantExecReadToRole);

    }
    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      change.AddScript(DbScriptType.TableRename, "EXEC SYS.SP_RENAME '{0}.{1}' , '{2}'", oldTable.Schema, oldTable.TableName, newTable.TableName);
    }

    public override void BuildColumnSetDefaultValuesSql(DbObjectChange change, DbColumnInfo column) {
      const string sqlUpdateValues = "UPDATE {0} SET \"{1}\" = {2} WHERE \"{1}\" IS NULL;";
      var fullTableRef = column.Table.FullName;
      change.AddScript(DbScriptType.ColumnInit, sqlUpdateValues, fullTableRef, column.ColumnName, column.TypeInfo.InitExpression);
    }
    
    public override void BuildIndexAddSql(DbObjectChange change, DbKeyInfo key) {
      const string CreateIndexTemplate = @"
CREATE {0} {1} INDEX {2}  
  ON {3} ( {4} )
  {5}
  {6}
";
      var unique = key.KeyType.IsSet(KeyType.Unique) ? "UNIQUE" : string.Empty;
      string clustered = GetClusteredExpression(key);
      var indexFields = key.KeyColumns.GetSqlNameListWithOrderSpec();
      var qKeyName = '"' + key.Name + '"';
      string includeList = string.Empty;
      if(key.IncludeColumns.Count > 0)
        includeList = "INCLUDE (" + key.IncludeColumns.GetSqlNameList() + ")";
      string wherePred = string.Empty;
      if(!string.IsNullOrWhiteSpace(key.Filter))
        wherePred = "WHERE " + key.Filter;
      var phase = key.KeyType.IsSet(KeyType.Clustered) ? ApplyPhase.Early : ApplyPhase.Default; 
      change.AddScript(DbScriptType.IndexAdd, phase, CreateIndexTemplate, 
        unique, clustered, qKeyName, key.Table.FullName, indexFields, includeList, wherePred);
    }

    public override void BuildPrimaryKeyAddSql(DbObjectChange change, DbKeyInfo key) {
      var pkFields = key.KeyColumns.GetSqlNameList();
      var clustered = GetClusteredExpression(key); 
      change.AddScript(DbScriptType.PrimaryKeyAdd, "ALTER TABLE {0} ADD CONSTRAINT \"{1}\" PRIMARY KEY {2} ({3});", key.Table.FullName, key.Name, clustered, pkFields);
    }

    public override void BuildRefConstraintAddSql(DbObjectChange change, DbRefConstraintInfo refConstraint) {
      const string sqlTemplate = @"ALTER TABLE {0} 
  ADD CONSTRAINT ""{1}"" FOREIGN KEY ({2}) REFERENCES {3} ({4}) {5};";
      var srcTable = refConstraint.FromKey.Table;
      var targetTable = refConstraint.ToKey.Table;
      var fullSrcTableRef = srcTable.FullName;
      var fullTargetTableRef = targetTable.FullName;
      var srcCols = refConstraint.FromKey.KeyColumns.GetSqlNameList();
      var targetCols = refConstraint.ToKey.KeyColumns.GetSqlNameList();
      bool cascade = refConstraint.OwnerReference.FromMember.Flags.IsSet(EntityMemberFlags.CascadeDelete);
      var onDeleteClause = cascade ? "ON DELETE CASCADE" : string.Empty;
      change.AddScript(DbScriptType.RefConstraintAdd, sqlTemplate, fullSrcTableRef, refConstraint.FromKey.Name, srcCols, fullTargetTableRef, targetCols, onDeleteClause);
    }

    //ALTER TABLE employees DROP COLUMN "employee_num";
    public override void BuildColumnDropSql(DbObjectChange change, DbColumnInfo column) {
      if(!string.IsNullOrEmpty(column.DefaultExpression))
        change.AddScript(DbScriptType.ColumnModify, "ALTER TABLE {0} DROP CONSTRAINT \"{1}\";", column.Table.FullName, column.DefaultConstraintName);
      //Note: the column drop comes after table-rename, so it might be table is already renamed, and we have to get its new name
      var tableName = column.Table.Peer.FullName; //new name if renamed
      change.AddScript(DbScriptType.ColumnDrop, "ALTER TABLE {0} DROP COLUMN \"{1}\";", tableName, column.ColumnName);
    }

    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      change.AddScript(DbScriptType.ColumnRename, "EXEC SYS.SP_RENAME '{0}.{1}.{2}' , '{3}', 'COLUMN'",
          oldColumn.Table.Schema, oldColumn.Table.TableName, oldColumn.ColumnName, newColumn.ColumnName);
    }

    //Dropping objects -----------------------------------------------------------------------------------------------------
    public override void BuildDatabaseDropSql(DbObjectChange change, string name) {
      change.AddScript(DbScriptType.DatabaseDrop, "DROP DATABASE \"{0}\";", name);
    }
    public override void BuildSchemaDropSql(DbObjectChange change, string schema) {
      change.AddScript(DbScriptType.SchemaDrop, "DROP SCHEMA \"{0}\";", schema);
    }//method

    public override void BuildTableDropSql(DbObjectChange change, DbTableInfo table) {
      change.AddScript(DbScriptType.TableDrop, "DROP TABLE {0}", table.FullName);
    }//method

    public override void BuildStoredProcDropSql(DbObjectChange change, DbCommandInfo command) {
      change.AddScript(DbScriptType.RoutineDrop, "DROP PROCEDURE {0};", command.FullCommandName);
    }


    public override void BuildRefConstraintDropSql(DbObjectChange change, DbRefConstraintInfo dbRefConstraint) {
      BuildTableConstraintDropSql(change, dbRefConstraint.FromKey);
    }

    public override void BuildIndexDropSql(DbObjectChange change, DbKeyInfo key) {
      //for indexes on DB views clustered index must be dropped last and created first
      var applyPhase = key.KeyType.IsSet(KeyType.Clustered) ? ApplyPhase.Late : ApplyPhase.Default; 
      change.AddScript(DbScriptType.IndexDrop, applyPhase, "DROP INDEX \"{0}\" ON {1};", key.Name, key.Table.FullName);
    }

    public override void BuildTableConstraintDropSql(DbObjectChange change, DbKeyInfo key) {
      change.AddScript(DbScriptType.TableConstraintDrop, "ALTER TABLE {0} DROP CONSTRAINT \"{1}\";", key.Table.FullName, key.Name);
    }

    public override void BuildSequenceAddSql(DbObjectChange change, DbSequenceInfo sequence) {
      const string sqlCreateTemplate = "CREATE Sequence {0} AS {1} START WITH {2} INCREMENT BY {3};";
      const string sqlGrantTemplate = "Grant  UPDATE on {0} to {1};";
      change.AddScript(DbScriptType.SequenceAdd, sqlCreateTemplate, sequence.FullName, sequence.DbType.SqlTypeSpec,
          sequence.StartValue, sequence.Increment);
      //Grant permission to UPDATE
      var updateRole = this.Settings.GrantExecWriteToRole;
      if (!string.IsNullOrWhiteSpace(updateRole))
        change.AddScript(DbScriptType.Grant, sqlGrantTemplate, sequence.FullName, updateRole); 
    }

    public override void BuildCustomTypeAddSql(DbObjectChange change, DbCustomTypeInfo typeInfo) {
      var sqlCreateTemplate = "CREATE TYPE {0} AS TABLE ([Value] Sql_Variant);";
      var sqlGrantTemplate = "Grant EXECUTE on TYPE::{0} to {1};";
      change.AddScript(DbScriptType.CustomTypeAdd, sqlCreateTemplate, typeInfo.FullName);
      if (!string.IsNullOrWhiteSpace(Settings.GrantExecReadToRole))
        change.AddScript(DbScriptType.CustomTypeAdd, sqlGrantTemplate, typeInfo.FullName, Settings.GrantExecReadToRole);
      if (!string.IsNullOrWhiteSpace(Settings.GrantExecWriteToRole) && Settings.GrantExecWriteToRole != Settings.GrantExecReadToRole)
        change.AddScript(DbScriptType.CustomTypeAdd, sqlGrantTemplate, typeInfo.FullName, Settings.GrantExecWriteToRole);
    }

    public override void BuildCustomTypeDropSql(DbObjectChange change, DbCustomTypeInfo typeInfo) {
      // We drop only Vita_* automatic types
      if (typeInfo.Name.StartsWith("Vita_"))
        change.AddScript(DbScriptType.CustomTypeDrop, "DROP TYPE {0};", typeInfo.FullName);
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

    private string GetClusteredExpression(DbKeyInfo key) {
      var clustered = key.KeyType.IsSet(KeyType.Clustered) ? "CLUSTERED" : "NONCLUSTERED";
      return clustered;
    }

  }
}
