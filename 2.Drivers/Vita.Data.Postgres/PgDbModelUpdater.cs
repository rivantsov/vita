using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Upgrades;

namespace Vita.Data.Postgres {
  public class PgDbModelUpdater : DbModelUpdater {

    public PgDbModelUpdater(DbSettings settings) : base(settings) { }

    public override void BuildColumnModifySql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      if(ShouldResetNullsToDefault(column))
        BuildColumnSetDefaultValuesSql(change, column);
      // In Pg you modify column one aspect at a time; setting TYPE and Nullable requires 2 calls
      change.AddScript(DbScriptType.ColumnModify, "ALTER TABLE {0} ALTER COLUMN \"{1}\" TYPE {2};",
        column.Table.FullName, column.ColumnName, column.TypeInfo.SqlTypeSpec);
      var nullStr = column.Flags.IsSet(DbColumnFlags.Nullable) ? "DROP NOT NULL" : "SET NOT NULL";
      change.AddScript(DbScriptType.ColumnSetupComplete, "ALTER TABLE {0} ALTER COLUMN \"{1}\" {2};", 
        column.Table.FullName, column.ColumnName, nullStr);
    }

    // Another trouble - when deleting routine, you have to provide proc name AND list of parameter types -
    // even if you have a single proc with this name; reason - proc name overloading.
    // You might have 2+ procs with the same name but different parameter lists, so you MUST always reference proc
    // using name and parameter types - even if you have just one defined. Seriously?!!!
    public override void BuildStoredProcDropSql(DbObjectChange change, DbCommandInfo command) {
      if(command.CustomTag == null) { //try to recover it
        var inpParams = command.Parameters.Where(p => p.Direction != ParameterDirection.Output);
        command.CustomTag = string.Join(", ", inpParams.Select(p => p.TypeInfo.SqlTypeSpec));
      }
      var funcRef = string.Format(@"{0}.""{1}""({2})", command.Schema, command.CommandName, command.CustomTag);
      change.AddScript(DbScriptType.RoutineDrop, "DROP FUNCTION {0};", funcRef); 
    }

    public override void BuildPrimaryKeyAddSql(DbObjectChange change, DbKeyInfo key) {
      base.BuildPrimaryKeyAddSql(change, key);
      if(key.KeyType.IsSet(KeyType.Clustered))
        change.AddScript(DbScriptType.PrimaryKeyAdd, "ALTER TABLE {0} CLUSTER ON \"{1}\";", key.Table.FullName, key.Name);
    }

    public override void BuildIndexAddSql(DbObjectChange change, DbKeyInfo key) {
      base.BuildIndexAddSql(change, key);
      if(key.KeyType.IsSet(KeyType.Clustered))
        change.AddScript(DbScriptType.IndexAdd,"ALTER TABLE {0} CLUSTER ON \"{1}\";", key.Table.FullName, key.Name);
    }

    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      change.AddScript(DbScriptType.TableRename, "ALTER TABLE {0} RENAME TO \"{1}\" ;", oldTable.FullName, newTable.TableName);
    }

    public override void BuildIndexDropSql(DbObjectChange change, DbKeyInfo key) {
      change.AddScript(DbScriptType.IndexDrop, "DROP INDEX \"{0}\".\"{1}\";", key.Table.Schema, key.Name);
    }

    public override void BuildViewAddSql(DbObjectChange change, DbTableInfo view) {
      const string createViewTemplate =
@"CREATE {0} VIEW {1}  AS 
  {2};
  COMMENT ON {0} VIEW {1} IS '{3}';
"; //notice - no ';' at the end, SQL must have it already
      // For indexed views add 'MATERIALIZED' attribute
      var matz = view.IsMaterializedView ? "MATERIALIZED" : string.Empty;
      change.AddScript(DbScriptType.ViewAdd, createViewTemplate, matz, view.FullName, view.ViewSql, view.ViewHash);
    }

    public override void BuildViewDropSql(DbObjectChange change, DbTableInfo view) {
      var matzed = view.IsMaterializedView ? "MATERIALIZED" : string.Empty;
      change.AddScript(DbScriptType.ViewDrop, "DROP {0} VIEW {1};", matzed, view.FullName);
      //base.BuildViewDropSql(change, view);
    }
    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      change.AddScript(DbScriptType.ColumnRename, "ALTER TABLE {0} RENAME COLUMN \"{1}\" TO \"{2}\";", newColumn.Table.FullName, oldColumn.ColumnName, newColumn.ColumnName);
    }

    public override void BuildSequenceAddSql(DbObjectChange change, DbSequenceInfo sequence) {
      var start = (sequence.StartValue < 1) ? 1 : sequence.StartValue; 
      const string sqlTemplate = "CREATE Sequence {0} START WITH {1} INCREMENT BY {2};";
      change.AddScript(DbScriptType.SequenceAdd, sqlTemplate, sequence.FullName,  start, sequence.Increment);
    }
    public override void BuildSequenceDropSql(DbObjectChange change, DbSequenceInfo sequence) {
      // PG creates sequences for identity columns, these should not be dropped explicitly; 
      // we do sequence drop after table drop, so we add check for existense
      change.AddScript(DbScriptType.SequenceDrop, "DROP SEQUENCE IF EXISTS {0}", sequence.FullName);
    }//method

    protected override string GetColumnSpec(DbColumnInfo column, DbScriptOptions options) {
      if(column.Flags.IsSet(DbColumnFlags.Identity)) {
        if(column.TypeInfo.SqlTypeSpec == "bigint")
          return string.Format(@"""{0}"" BIGSERIAL ", column.ColumnName);
        else
          return string.Format(@"""{0}"" SERIAL ", column.ColumnName);
      }
      return base.GetColumnSpec(column, options);
    }
  
  }
}
