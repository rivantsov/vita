using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Data.Upgrades;

namespace Vita.Data.Postgres {
  public class PgDbModelUpdater : DbModelUpdater {

    public PgDbModelUpdater(DbSettings settings) : base(settings) { }

    public override void BuildColumnModifySql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      if(ShouldResetNullsToDefault(column))
        BuildColumnSetDefaultValuesSql(change, column);
      // In Pg you modify column one aspect at a time; setting TYPE and Nullable requires 2 calls
      change.AddScript(DbScriptType.ColumnModify, $"ALTER TABLE {column.Table.FullName} ALTER COLUMN {column.ColumnNameQuoted} TYPE {column.TypeInfo.SqlTypeSpec};");
      var nullStr = column.Flags.IsSet(DbColumnFlags.Nullable) ? " DROP NOT NULL" : " SET NOT NULL";
      change.AddScript(DbScriptType.ColumnSetupComplete, $"ALTER TABLE {column.Table.FullName} ALTER COLUMN {column.ColumnNameQuoted}{nullStr};");
    }

    public override void BuildPrimaryKeyAddSql(DbObjectChange change, DbKeyInfo key) {
      base.BuildPrimaryKeyAddSql(change, key);
      if(key.KeyType.IsSet(KeyType.Clustered)) {
        var kn = QuoteName(key.Name); 
        change.AddScript(DbScriptType.PrimaryKeyAdd, $"ALTER TABLE {key.Table.FullName} CLUSTER ON {kn};");
      }
    }

    public override void BuildIndexAddSql(DbObjectChange change, DbKeyInfo key) {
      base.BuildIndexAddSql(change, key);
      if(key.KeyType.IsSet(KeyType.Clustered)) {
        var kn = QuoteName(key.Name); 
        change.AddScript(DbScriptType.IndexAdd, $"ALTER TABLE {key.Table.FullName} CLUSTER ON {kn};");
      }
    }

    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      var newName = QuoteName(newTable.TableName); 
      change.AddScript(DbScriptType.TableRename, $"ALTER TABLE {oldTable.FullName} RENAME TO {newName};");
    }

    public override void BuildIndexDropSql(DbObjectChange change, DbKeyInfo key) {
      var fullName = QuoteName(key.Table.Schema) + "." + QuoteName(key.Name); 
      change.AddScript(DbScriptType.IndexDrop, $"DROP INDEX {fullName};");
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

    public override void BuildViewDropSql(DbObjectChange change, DbTableInfo view) {
      var matzed = view.IsMaterializedView ? "MATERIALIZED" : string.Empty;
      change.AddScript(DbScriptType.ViewDrop, $"DROP {matzed} VIEW {view.FullName};");
      //base.BuildViewDropSql(change, view);
    }

    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      change.AddScript(DbScriptType.ColumnRename, 
        $"ALTER TABLE {newColumn.Table.FullName} RENAME COLUMN {oldColumn.ColumnNameQuoted} TO {newColumn.ColumnNameQuoted};");
    }

    public override void BuildSequenceAddSql(DbObjectChange change, DbSequenceInfo sequence) {
      var start = (sequence.StartValue < 1) ? 1 : sequence.StartValue; 
      change.AddScript(DbScriptType.SequenceAdd, 
        $"CREATE Sequence {sequence.FullName} START WITH {start} INCREMENT BY {sequence.Increment};");
    }

    public override void BuildSequenceDropSql(DbObjectChange change, DbSequenceInfo sequence) {
      // PG creates sequences for identity columns, these should not be dropped explicitly; 
      // we do sequence drop after table drop, so we add check for existense
      change.AddScript(DbScriptType.SequenceDrop, $"DROP SEQUENCE IF EXISTS {sequence.FullName}");
    }//method

    protected override string GetColumnSpec(DbColumnInfo column, DbScriptOptions options) {
      if(column.Flags.IsSet(DbColumnFlags.Identity)) {
        if(column.TypeInfo.SqlTypeSpec == "bigint")
          return $"{column.ColumnNameQuoted} BIGSERIAL ";
        else
          return $"{column.ColumnNameQuoted} SERIAL ";
      }
      return base.GetColumnSpec(column, options);
    }
  
  }
}
