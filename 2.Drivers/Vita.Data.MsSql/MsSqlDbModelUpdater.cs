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

namespace Vita.Data.MsSql {

  public partial class MsSqlDbModelUpdater : DbModelUpdater {

    public MsSqlDbModelUpdater(DbSettings settings) : base(settings) { }


    public override void BuildViewAddSql(DbObjectChange change, DbTableInfo view) {
      // For materialized views add 'With SCHEMABINDING' attribute
      var attrs = view.IsMaterializedView ? "WITH SCHEMABINDING" : string.Empty;
      //notice - no ';' at the end, SQL must have it already
      var script = $@"CREATE VIEW {view.FullName} {attrs}  AS 
  {view.ViewSql}"; 
      change.AddScript(DbScriptType.ViewAdd, script);
      //Grant Select 
      if (!string.IsNullOrWhiteSpace(Settings.GrantExecReadToRole))
        change.AddScript(DbScriptType.Grant, $"GRANT SELECT ON {view.FullName} TO [{Settings.GrantExecReadToRole}];");

    }
    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      change.AddScript(DbScriptType.TableRename, $"EXEC SYS.SP_RENAME '{oldTable.Schema}.{oldTable.TableName}' , '{newTable.TableName}'");
    }

    public override void BuildIndexAddSql(DbObjectChange change, DbKeyInfo key) {
      var driver = this.Settings.Driver;
      var unique = key.KeyType.IsSet(KeyType.Unique) ? "UNIQUE" : string.Empty;
      string clustered = GetClusteredExpression(key);
      string indexFields;
      if(driver.Supports(DbFeatures.OrderedColumnsInIndexes))
        indexFields = key.KeyColumns.GetSqlNameListWithOrderSpec();
      else
        indexFields = key.KeyColumns.GetSqlNameList();
      var qKeyName = QuoteName(key.Name);
      string includeList = (key.IncludeColumns.Count == 0) ? string.Empty : "INCLUDE (" + key.IncludeColumns.GetSqlNameList() + ")";
      string wherePred = (key.Filter == null) ? string.Empty: "WHERE " + key.Filter.DefaultSql;
      var script = $@"
CREATE {unique} {clustered} INDEX {qKeyName}  
  ON {key.Table.FullName} ( {indexFields} )
  {includeList}
  {wherePred}
;";
      var phase = key.KeyType.IsSet(KeyType.Clustered) ? ApplyPhase.Early : ApplyPhase.Default;
      change.AddScript(DbScriptType.IndexAdd, phase, script);
    }



    public override void BuildPrimaryKeyAddSql(DbObjectChange change, DbKeyInfo key) {
      var pkFields = key.KeyColumns.GetSqlNameList();
      var clustered = GetClusteredExpression(key);
      var qn = QuoteName(key.Name); 
      change.AddScript(DbScriptType.PrimaryKeyAdd, $"ALTER TABLE {key.Table.FullName} ADD CONSTRAINT {qn} PRIMARY KEY {clustered} ({pkFields});");
    }

    public override void BuildRefConstraintAddSql(DbObjectChange change, DbRefConstraintInfo refConstraint) {
      var srcTable = refConstraint.FromKey.Table;
      var targetTable = refConstraint.ToKey.Table;
      var srcCols = refConstraint.FromKey.KeyColumns.GetSqlNameList();
      var targetCols = refConstraint.ToKey.KeyColumns.GetSqlNameList();
      bool cascade = refConstraint.OwnerReference.FromMember.Flags.IsSet(EntityMemberFlags.CascadeDelete);
      var onDeleteClause = cascade ? "ON DELETE CASCADE" : string.Empty;
      var cn = QuoteName(refConstraint.FromKey.Name);
      var script = $@"ALTER TABLE {srcTable.FullName} 
  ADD CONSTRAINT {cn} FOREIGN KEY ({srcCols}) REFERENCES {targetTable.FullName} ({targetCols}) {onDeleteClause};";
      change.AddScript(DbScriptType.RefConstraintAdd, script);
    }

    //ALTER TABLE employees DROP COLUMN "employee_num";
    public override void BuildColumnDropSql(DbObjectChange change, DbColumnInfo column) {
      if(!string.IsNullOrEmpty(column.DefaultExpression)) {
        var cn = QuoteName(column.DefaultConstraintName); 
        change.AddScript(DbScriptType.ColumnModify, $"ALTER TABLE {column.Table.FullName} DROP CONSTRAINT {cn};");
      }
      //Note: the column drop comes after table-rename, so it might be table is already renamed, and we have to get its new name
      var tableName = column.Table.Peer.FullName; //new name if renamed
      change.AddScript(DbScriptType.ColumnDrop, $"ALTER TABLE {tableName} DROP COLUMN {column.ColumnNameQuoted};");
    }

    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      var sch = oldColumn.Table.Schema;
      var tn = oldColumn.Table.TableName;
      var oldColName = oldColumn.ColumnName;
      var newColName = newColumn.ColumnName;
      change.AddScript(DbScriptType.ColumnRename, $"EXEC SYS.SP_RENAME '{sch}.{tn}.{oldColName}' , '{newColName}', 'COLUMN'");
    }

    //Dropping objects -----------------------------------------------------------------------------------------------------
    public override void BuildIndexDropSql(DbObjectChange change, DbKeyInfo key) {
      //for indexes on DB views clustered index must be dropped last and created first
      var applyPhase = key.KeyType.IsSet(KeyType.Clustered) ? ApplyPhase.Late : ApplyPhase.Default;
      var qn = QuoteName(key.Name);
      change.AddScript(DbScriptType.IndexDrop, applyPhase, $"DROP INDEX {qn} ON {key.Table.FullName};");
    }

    public override void BuildSequenceAddSql(DbObjectChange change, DbSequenceInfo sequence) {
      const string sqlCreateTemplate = "CREATE Sequence {0} AS {1} START WITH {2} INCREMENT BY {3};";
      const string sqlGrantTemplate = "Grant  UPDATE on {0} to {1};";
      var typeName = GetIntDbTypeName(sequence.Definition.DataType);
      change.AddScript(DbScriptType.SequenceAdd, sqlCreateTemplate, sequence.FullName, typeName,
          sequence.StartValue, sequence.Increment);
      //Grant permission to UPDATE
      var updateRole = this.Settings.GrantExecWriteToRole;
      if (!string.IsNullOrWhiteSpace(updateRole))
        change.AddScript(DbScriptType.Grant, sqlGrantTemplate, sequence.FullName, updateRole); 
    }
    private string GetIntDbTypeName(Type type) {
      if(type == typeof(long) || type == typeof(ulong))
        return "BIGINT";
      else
        return "INT";
    }

    public override void BuildCustomTypeAddSql(DbObjectChange change, DbCustomTypeInfo typeInfo) {
      var tn = typeInfo.FullName; 
      change.AddScript(DbScriptType.CustomTypeAdd, $"CREATE TYPE {tn} AS TABLE ([Value] Sql_Variant);");
      if (!string.IsNullOrWhiteSpace(Settings.GrantExecReadToRole))
        change.AddScript(DbScriptType.CustomTypeAdd, $"Grant EXECUTE on TYPE::{tn} to [{Settings.GrantExecReadToRole}];");
      if (!string.IsNullOrWhiteSpace(Settings.GrantExecWriteToRole) && Settings.GrantExecWriteToRole != Settings.GrantExecReadToRole)
        change.AddScript(DbScriptType.CustomTypeAdd, $"Grant EXECUTE on TYPE::{tn} to [{Settings.GrantExecWriteToRole}];");
    }

    public override void BuildCustomTypeDropSql(DbObjectChange change, DbCustomTypeInfo typeInfo) {
      // We drop only Vita_* automatic types
      if (typeInfo.Name.StartsWith("Vita_"))
        change.AddScript(DbScriptType.CustomTypeDrop, $"DROP TYPE {typeInfo.FullName};");
    }

    protected override string GetColumnSpec(DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      var typeStr = column.TypeInfo.DbTypeSpec;
      var nullable = options.IsSet(DbScriptOptions.ForceNull) || column.Flags.IsSet(DbColumnFlags.Nullable);
      var nullStr = nullable ? "NULL" : "NOT NULL";
      var idStr = string.Empty;
      bool isNew = column.Peer == null;
      if(isNew && column.Flags.IsSet(DbColumnFlags.Identity)) 
        idStr = "IDENTITY(1,1)";
      string defaultStr = null;
      //Default constraint can be set only on new columns in SQL server
      if(!string.IsNullOrWhiteSpace(column.DefaultExpression) && isNew)
        defaultStr = "DEFAULT " + column.DefaultExpression;
      var spec = $" {column.ColumnNameQuoted} {typeStr} {idStr} {defaultStr} {nullStr}"; 
      return spec;
    }

    private string GetClusteredExpression(DbKeyInfo key) {
      var clustered = key.KeyType.IsSet(KeyType.Clustered) ? "CLUSTERED" : "NONCLUSTERED";
      return clustered;
    }

  }
}
