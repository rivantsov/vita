using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities;
using Vita.Entities.Model;
using System.Data;

namespace Vita.Data.Driver {

  //Builds DDL (Data Definition Language) SQL statements - for changing database schema (tables, columns, indexes, etc)
  public class DbModelUpdater {
    protected DbSettings Settings;
    public static bool Test_RandomizeInitialSchemaUpdatesOrder; //for testing only, to test how well ordering works

    public DbModelUpdater(DbSettings settings) {
      Settings = settings; 
    }

    public virtual void BuildScripts(DbUpgradeInfo upgradeInfo) {
      var allChanges = new List<DbObjectChange>(upgradeInfo.NonTableChanges);
      allChanges.AddRange(upgradeInfo.TableChanges.SelectMany(tc => tc.Changes));
      foreach(var change in allChanges) {
        BuildScripts(change);
        upgradeInfo.AllScripts.AddRange(change.Scripts);
      }
      if(Test_RandomizeInitialSchemaUpdatesOrder)
        RandomHelper.RandomizeListOrder(upgradeInfo.AllScripts);
    }

    public virtual void BuildScripts(List<DbObjectChange> allChanges) {
      foreach(var change in allChanges) {
        BuildScripts(change);
      }
    }


    /// <summary>
    /// Builds SQL statement(s) implementing DB model change action; dispatches the call to one of the virtual methods
    /// building scripts for specific action. 
    /// </summary>
    /// <param name="change">Db model change object.</param>
    /// <remarks>Inherited class in DB Vendor-specific driver should override virtual methods for specific actions
    /// and provide vendor-specific SQLs. If the method returns null as script, it is considered not supported action, and 
    /// no script is added. 
    /// </remarks>
    public virtual void BuildScripts(DbObjectChange change) {
      switch(change.ObjectType) {
        case DbObjectType.Schema:
          var schObj = (DbSchemaInfo) change.OldObject;
          if (schObj != null) 
            BuildSchemaDropSql(change, schObj.Schema);
          schObj = (DbSchemaInfo)change.NewObject;
          if (schObj != null)
            BuildSchemaAddSql(change, schObj.Schema);
          break; 

        case DbObjectType.Table:

          switch(change.ChangeType) {
            case DbObjectChangeType.Add:
              BuildTableAddSql(change, (DbTableInfo)change.NewObject);
              break; 
            case DbObjectChangeType.Drop:
              BuildTableDropSql(change, (DbTableInfo)change.OldObject);
              break; 
            case DbObjectChangeType.Modify:
              //nothing to do, everyting is in child actions
              break; 
            case DbObjectChangeType.Rename:
              BuildTableRenameSql(change, (DbTableInfo) change.OldObject, (DbTableInfo) change.NewObject);
              break; 
          } //switch changeType
          break; 

        case DbObjectType.Column:
          switch(change.ChangeType) {
            case DbObjectChangeType.Add:
              BuildColumnAddSqlSafe(change, (DbColumnInfo)change.NewObject);
              break;
            case DbObjectChangeType.Drop:
              BuildColumnDropSql(change, (DbColumnInfo)change.OldObject);
              break;
            case DbObjectChangeType.Rename:
              BuildColumnRenameSql(change, (DbColumnInfo)change.OldObject, (DbColumnInfo)change.NewObject);
              break;
            case DbObjectChangeType.Modify:
              var newCol = (DbColumnInfo)change.NewObject;
              var oldCol = (DbColumnInfo)change.OldObject;
              bool changesToNonNull = oldCol.Flags.IsSet(DbColumnFlags.Nullable) && !newCol.Flags.IsSet(DbColumnFlags.Nullable);
              if (changesToNonNull) {
                var oldSpec = GetColumnSpec(oldCol);
                var newSpec = GetColumnSpec(newCol, DbScriptOptions.ForceNull);
                if (newSpec != oldSpec) //if anything changes except Nullable, then add column change
                  BuildColumnModifySql(change, newCol, DbScriptOptions.ForceNull);
                BuildColumnModifySql(change, newCol, DbScriptOptions.CompleteColumnSetup);
              } else 
                BuildColumnModifySql(change, newCol);
              break; 
          }
          break; 

        case DbObjectType.Key:
          var oldKey = (DbKeyInfo) change.OldObject;
          var newKey = (DbKeyInfo) change.NewObject;
          var table = (newKey ?? oldKey).Table; 
          var keyType = (oldKey == null) ? newKey.KeyType : oldKey.KeyType;
          if(keyType.IsSet(KeyType.ForeignKey)) {
            //nothing to do, we handle it in constraints
          } else if(keyType.IsSet(KeyType.PrimaryKey) && table.Kind == EntityKind.Table) {
            if (oldKey != null)
              BuildTableConstraintDropSql(change, oldKey);
            if (newKey != null)
              BuildPrimaryKeyAddSql(change, newKey);
          } else if(keyType.IsSet(KeyType.Index)) {
            if (oldKey != null)
              BuildIndexDropSql(change, oldKey);
            if (newKey != null)
              BuildIndexAddSql(change, newKey);
          } //else
          break; 

        case DbObjectType.RefConstraint:
          var dbConstr = (DbRefConstraintInfo)change.OldObject;
          if(dbConstr != null)
            BuildRefConstraintDropSql(change, dbConstr);
          dbConstr = (DbRefConstraintInfo)change.NewObject;
          if(dbConstr != null)
            BuildRefConstraintAddSql(change, dbConstr);
          break; 

        case DbObjectType.View:
          var view = (DbTableInfo)change.OldObject;
          if(view != null)
            BuildViewDropSql(change, view);
          view = (DbTableInfo)change.NewObject;
          if(view != null)
            BuildViewAddSql(change, view);
          break;

        case DbObjectType.Sequence:
          var seq = (DbSequenceInfo)change.OldObject;
          if (seq != null)
            BuildSequenceDropSql(change, seq);
          seq = (DbSequenceInfo)change.NewObject;
          if (seq != null)
            BuildSequenceAddSql(change, seq);
          break;

        case DbObjectType.UserDefinedType:
          var ctype = (DbCustomTypeInfo)change.OldObject;
          if (ctype != null)
            BuildCustomTypeDropSql(change, ctype);
          ctype = (DbCustomTypeInfo)change.NewObject;
          if (ctype != null)
            BuildCustomTypeAddSql(change, ctype);
          break;


        case DbObjectType.Other:
          break; 
      }
      if (change.Scripts.Count == 0)
        BuildMissingScript(change);
    }

    /// <summary>Provides vendor-specific implementation for DB model change. 
    /// Called if no scripts were provided for a change by specific SQL-generating method. </summary>
    /// <param name="change">DB model change object.</param>
    public virtual void BuildMissingScript(DbObjectChange change) {

    }

    public virtual void BuildDatabaseAddSql(DbObjectChange change, string name) {
      var qn = QuoteName(name); 
      change.AddScript(DbScriptType.DatabaseAdd, $"CREATE DATABASE {qn}");
    }

    public virtual void BuildSchemaAddSql(DbObjectChange change, string schema) {
      var qn = QuoteName(schema); 
      change.AddScript(DbScriptType.SchemaAdd, $"CREATE SCHEMA {qn};");
    }//method

    public virtual void BuildTableAddSql(DbObjectChange change, DbTableInfo table) {
      var colSpecList = table.Columns.Select(c => GetColumnSpec(c, DbScriptOptions.NewColumn));
      var columnSpecs = string.Join("," + Environment.NewLine, colSpecList);
      var script =
$@"CREATE TABLE {table.FullName} (
{columnSpecs}
); ";
      change.AddScript(DbScriptType.TableAdd, script);
    }//method

    // All columns are added as nullable, to allow for existing rows be filled with nulls
    // Then extra step sets default values for types (zeros), and then column is modified to NOT NULL
    protected virtual void BuildColumnAddSqlSafe(DbObjectChange change, DbColumnInfo column) {
      var nullable = column.Flags.IsSet(DbColumnFlags.Nullable);
      var noDefault = string.IsNullOrEmpty(column.DefaultExpression);
      var isIdentity = column.Flags.IsSet(DbColumnFlags.Identity);
      var forceNull = !nullable && noDefault && !isIdentity;
      var options = forceNull ? DbScriptOptions.ForceNull : DbScriptOptions.None;
      options |= DbScriptOptions.NewColumn;
      BuildColumnAddSql(change, column, options);
      if(forceNull) {
        if (ShouldResetNullsToDefault(column))
          BuildColumnSetDefaultValuesSql(change, column);
        BuildColumnModifySql(change, column, DbScriptOptions.CompleteColumnSetup);
      }
    }

    // ALTER TABLE employees ADD [employee_pwd] nvarchar(20) Null;
    // All columns are added as nullable, to allow for existing rows be filled with nulls
    // Then extra step columnInit sets default values for types (zeros), and then column is modified to NOT NULL
    public virtual void BuildColumnAddSql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options) {
      var colSpec = GetColumnSpec(column, options);
      change.AddScript(DbScriptType.ColumnAdd, $"ALTER TABLE {column.Table.FullName} ADD {colSpec};");
    }
    public virtual void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
      //Syntax for MySql and almost like Postgres
      change.AddScript(DbScriptType.TableRename, $"ALTER TABLE {oldTable.FullName} RENAME TO {newTable.FullName};");
    }

    public virtual void BuildColumnSetDefaultValuesSql(DbObjectChange change, DbColumnInfo column) {
      var fullTableRef = column.Table.FullName;
      var cn = column.ColumnNameQuoted;
      var init = column.TypeInfo.TypeDef.ColumnInit;
      change.AddScript(DbScriptType.ColumnInit, $"UPDATE {fullTableRef} SET {cn} = {init} WHERE {cn} IS NULL;");
    }

    public virtual void BuildIndexAddSql(DbObjectChange change, DbKeyInfo key) {
      var driver = this.Settings.Driver;
      var unique = key.KeyType.IsSet(KeyType.Unique) ? "UNIQUE" : string.Empty;
      string indexFields;
      if(driver.Supports(DbFeatures.OrderedColumnsInIndexes))
        indexFields = key.KeyColumns.GetSqlNameListWithOrderSpec();
      else
        indexFields = key.KeyColumns.GetSqlNameList();
      var qKeyName = '"' + key.Name + '"';
      string includeList = string.Empty;
      if(key.IncludeColumns.Count > 0 && driver.Supports(DbFeatures.IncludeColumnsInIndexes))
        includeList = "INCLUDE (" + key.IncludeColumns.GetSqlNameList() + ")";
      string wherePred = string.Empty;
      if(key.Filter != null && driver.Supports(DbFeatures.FilterInIndexes))
        wherePred = "WHERE " + key.Filter.DefaultSql;
      var script = $@"
CREATE {unique} INDEX {qKeyName}  
  ON {key.Table.FullName} ( {indexFields} )
  {includeList}
  {wherePred}
";
      change.AddScript(DbScriptType.IndexAdd, script);
    }

    public virtual void BuildPrimaryKeyAddSql(DbObjectChange change, DbKeyInfo key) {
      var pkFields = key.KeyColumns.GetSqlNameList();
      var tname = key.Table.FullName;
      var keyName = QuoteName(key.Name); 
      change.AddScript(DbScriptType.PrimaryKeyAdd, $"ALTER TABLE {tname} ADD CONSTRAINT {keyName} PRIMARY KEY ({pkFields})");
    }

    public virtual void BuildRefConstraintAddSql(DbObjectChange change, DbRefConstraintInfo refConstraint) {
      var srcTable = refConstraint.FromKey.Table;
      var targetTable = refConstraint.ToKey.Table;
      var fullSrcTableRef = srcTable.FullName;
      var fullTargetTableRef = targetTable.FullName;
      var srcCols = refConstraint.FromKey.KeyColumns.GetSqlNameList();
      var targetCols = refConstraint.ToKey.KeyColumns.GetSqlNameList();
      bool cascade = refConstraint.OwnerReference.FromMember.Flags.IsSet(EntityMemberFlags.CascadeDelete);
      var onDeleteClause = cascade ? " ON DELETE CASCADE" : string.Empty;
      var keyName = QuoteName(refConstraint.FromKey.Name);
      var script = $@"ALTER TABLE {srcTable.FullName} ADD CONSTRAINT {keyName} FOREIGN KEY 
  ({srcCols}) REFERENCES 
  {fullTargetTableRef} ({targetCols}){onDeleteClause};";
      change.AddScript(DbScriptType.RefConstraintAdd, script);
    }

    // Inject hash value, some servers preserve it, some don't
    public virtual void BuildViewAddSql(DbObjectChange change, DbTableInfo view) {
      var viewAddSql = 
$@"CREATE VIEW {view.FullName} AS 
{view.ViewSql}"; 
      change.AddScript(DbScriptType.ViewAdd, viewAddSql);
    }

    public virtual void BuildSequenceAddSql(DbObjectChange change, DbSequenceInfo sequence) {
    }


    //ALTER TABLE employees DROP COLUMN [employee_pwd];
    public virtual void BuildColumnDropSql(DbObjectChange change, DbColumnInfo column) {
      //Note: the column drop comes after table-rename, so it might be table is already renamed, and we have to get its new name
      var tableName = column.Table.Peer.FullName; //new name if renamed
      change.AddScript(DbScriptType.ColumnDrop, $"ALTER TABLE {tableName} DROP COLUMN {column.ColumnNameQuoted}");
    }

    public virtual void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      var serverType = Settings.Driver.ServerType; 
      Util.Throw($"Column renaming is not supported by DbDriver {serverType}.");
    }

    //ALTER TABLE employees ALTER COLUMN [employee_name] nvarchar(100) Null;
    public virtual void BuildColumnModifySql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      var scriptType = options.IsSet(DbScriptOptions.CompleteColumnSetup) ? DbScriptType.ColumnSetupComplete : DbScriptType.ColumnModify;
      if(ShouldResetNullsToDefault(column)) {
        BuildColumnSetDefaultValuesSql(change, column);
        scriptType = DbScriptType.ColumnSetupComplete;
      }
      var colSpec = GetColumnSpec(column);
      change.AddScript(scriptType, $"ALTER TABLE {column.Table.FullName} ALTER COLUMN {colSpec};");
    }

    protected bool ShouldResetNullsToDefault(DbColumnInfo column) {
      if(column.Flags.IsSet(DbColumnFlags.Nullable | DbColumnFlags.ForeignKey))
        return false;
      if(string.IsNullOrWhiteSpace(column.TypeInfo.TypeDef.ColumnInit)) //no init expression
        return false; 
      // (new) column or it is not nullable
      if(column.Peer == null) // it is new column
        return true;
      if(column.Peer.Flags.IsSet(DbColumnFlags.Nullable)) //old was nullable, but new is not nullable
        return true;
      return false; 
    }

    //Dropping objects -----------------------------------------------------------------------------------------------------
    public virtual void BuildDatabaseDropSql(DbObjectChange change, string name) {
      var qn = QuoteName(name); 
      change.AddScript(DbScriptType.DatabaseDrop, $"DROP DATABASE {qn};");
    }
    public virtual void BuildSchemaDropSql(DbObjectChange change, string schema) {
      var qs = QuoteName(schema); 
      change.AddScript(DbScriptType.SchemaDrop, $"DROP SCHEMA {qs};");
    }//method

    public virtual void BuildTableDropSql(DbObjectChange change, DbTableInfo table) {
      change.AddScript(DbScriptType.TableDrop, $"DROP TABLE {table.FullName}");
    }//method
    public virtual void BuildViewDropSql(DbObjectChange change, DbTableInfo view) {
      change.AddScript(DbScriptType.ViewDrop, $"DROP VIEW {view.FullName}");
    }//method

    public virtual void BuildSequenceDropSql(DbObjectChange change, DbSequenceInfo sequence) {
      change.AddScript(DbScriptType.SequenceDrop, $"DROP SEQUENCE {sequence.FullName}");
    }//method

    public virtual void BuildRefConstraintDropSql(DbObjectChange change, DbRefConstraintInfo dbRefConstraint) {
      BuildTableConstraintDropSql(change, dbRefConstraint.FromKey);
    }

    public virtual void BuildIndexDropSql(DbObjectChange change, DbKeyInfo key) {
      var qn = QuoteName(key.Name); 
      change.AddScript(DbScriptType.IndexDrop, $"DROP INDEX {qn} ON {key.Table.FullName};");
    }

    public virtual void BuildTableConstraintDropSql(DbObjectChange change, DbKeyInfo key) {
      var qn = QuoteName(key.Name);
      change.AddScript(DbScriptType.TableConstraintDrop, $"ALTER TABLE {key.Table.FullName} DROP CONSTRAINT {qn};");
    }

    public virtual void BuildCustomTypeAddSql(DbObjectChange change, DbCustomTypeInfo typeInfo) {
    }
    public virtual void BuildCustomTypeDropSql(DbObjectChange change, DbCustomTypeInfo typeInfo) {
    }

    //Helper methods
    protected virtual void BuildColumnRenameSqlWithAddDrop(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      //Add new column
      BuildColumnAddSql(change, newColumn, DbScriptOptions.ForceNull | DbScriptOptions.NewColumn);
      // copy data
      change.AddScript(DbScriptType.ColumnCopyValues, $"UPDATE {oldColumn.Table.FullName} SET {newColumn.ColumnNameQuoted} = {oldColumn.ColumnNameQuoted};");
      // finalize added column
      BuildColumnModifySql(change, newColumn, DbScriptOptions.CompleteColumnSetup);
      //drop old column
      BuildColumnDropSql(change, oldColumn);
    }


    protected virtual string GetColumnSpec(DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      var typeStr = column.TypeInfo.DbTypeSpec;
      var nullable = options.IsSet(DbScriptOptions.ForceNull) || column.Flags.IsSet(DbColumnFlags.Nullable);
      var nullStr = nullable ? "NULL" : "NOT NULL";
      string defaultStr = null;
      if(!string.IsNullOrWhiteSpace(column.DefaultExpression))
        defaultStr = "DEFAULT " + column.DefaultExpression;
      var spec = $" {column.ColumnNameQuoted} {typeStr} {defaultStr} {nullStr}";
      return spec;
    }

    protected void CheckDefaultConstraintName(DbColumnInfo column) {
      if(string.IsNullOrEmpty(column.DefaultExpression) || !string.IsNullOrWhiteSpace(column.DefaultConstraintName))
        return;
      var tbl = column.Table;
      column.DefaultConstraintName = "DEFAULT_" + tbl.Schema + "_" + tbl.TableName + "_" + column.ColumnName;
    }

    protected virtual string QuoteName(string name) {
      var dq = '"';
      return dq + name + dq;
    }


  }//class
}
