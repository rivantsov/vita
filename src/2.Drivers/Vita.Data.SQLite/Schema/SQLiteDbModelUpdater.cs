using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities.Model;

namespace Vita.Data.SQLite {
  public class SQLiteDbModelUpdater : DbModelUpdater {
    public SQLiteDbModelUpdater(DbSettings settings) : base(settings) { }

    public override void BuildPrimaryKeyAddSql(DbObjectChange change, Model.DbKeyInfo key) {
    }

    public override void BuildRefConstraintAddSql(DbObjectChange change, DbRefConstraintInfo refConstraint) {
    }

    //not supported
    public override void BuildRefConstraintDropSql(DbObjectChange change, DbRefConstraintInfo dbRefConstraint) {
    }

    public override void BuildTableConstraintDropSql(DbObjectChange change, DbKeyInfo key) {
    }
    //not supported
    public override void BuildColumnModifySql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
    }

    //Safe add by default adds column as nullable, initializes it with default value, then changes to non-nullable(it necessary)
    // SQLite does not support modifying columns, so we add column directly with DEFAULT clause for non-nullable columns
    protected override void BuildColumnAddSqlSafe(DbObjectChange change, DbColumnInfo column) {
      this.BuildColumnAddSql(change, column, DbScriptOptions.NewColumn);
    }
    public override void BuildColumnAddSql(DbObjectChange change, DbColumnInfo column, DbScriptOptions options) {
      var colSpec = GetColumnSpec(column, options);
      if(!column.Flags.IsSet(DbColumnFlags.Nullable)) {
        var dft = column.TypeInfo.TypeDef.ColumnInit;
        if (string.IsNullOrWhiteSpace(dft))
          dft = column.TypeInfo.TypeDef.ToLiteral(new byte[] {0}); 
        colSpec += " DEFAULT " + dft; 
          
      }
      //workaround for unit test with renaming table - ignore rename, use old table
      var tbl = column.Table;
      if (tbl.Peer != null)
        tbl = tbl.Peer; //use old table name
      change.AddScript(DbScriptType.ColumnAdd, $"ALTER TABLE {tbl.FullName} ADD {colSpec};");
    }

    //not supported; all we can do is nullify it; so if it is a FK it no longer holds target refs
    public override void BuildColumnDropSql(DbObjectChange change, DbColumnInfo column) {
      //Note: the column drop comes after table-rename, so it might be table is already renamed, and we have to get its new name
      var tableName = column.Table.Peer.FullName; //new name if renamed
      if (column.Flags.IsSet(DbColumnFlags.Nullable) && column.Flags.IsSet(DbColumnFlags.ForeignKey)) {
        change.AddScript(DbScriptType.ColumnInit, $"UPDATE {tableName} SET {column.ColumnNameQuoted} = NULL;");
      }
    }

    //Drop table - supported, but will fail if there's an old foreign key to this table. Dropping FKs is not supported, 
    // so we do not delete the table if there are foreign keys
    public override void BuildTableDropSql(DbObjectChange change, DbTableInfo table) {
      var refs = table.GetIncomingReferences();
      if(refs.Count > 0)
        return; 
      base.BuildTableDropSql(change, table);
    }

    //not supported
    public override void BuildTableRenameSql(DbObjectChange change, DbTableInfo oldTable, DbTableInfo newTable) {
    }


    //Default impl adds table name to the statement: "DROP INDEX <IndexName> ON <tableName>"; SQLite does not use table name
    public override void BuildIndexDropSql(DbObjectChange change, DbKeyInfo key) {
      var kn = QuoteName(key.Name);       
      change.AddScript(DbScriptType.IndexDrop, $"DROP INDEX {kn}");
    }

    public override void BuildTableAddSql(DbObjectChange change, DbTableInfo table) {
      var realColumns = GetTableColumns(table); 
      var specs = realColumns.Select(c => GetColumnSpec(c)).ToList();
      //Until now it was the same as default impl method in base class. Now we need to add Primary key and Foreign key constraints
      //Primary Key
      var pk = table.PrimaryKey;
      var col0 = pk.KeyColumns[0].Column;
      //Identity Primary Key is taken care of in GetColumnSpec
      // Note: looks like we need to declare identity PK in GetColumnSpec - SQLite is quite tricky in this way
      if (!col0.Flags.IsSet(DbColumnFlags.Identity)) {
        var strKeyCols = pk.KeyColumns.GetSqlNameList();
        var pkSpec = $"PRIMARY KEY({strKeyCols})";
        specs.Add(pkSpec);
      }
      //Foreign keys (ref constraints
      foreach(var refC in table.RefConstraints) {
        var strKeyCols = refC.FromKey.KeyColumns.GetSqlNameList();
        //find target table
        var strPkKeyCols = refC.ToKey.KeyColumns.GetSqlNameList();
        var tn = refC.ToKey.Table.FullName;
        var fkSpec = $"FOREIGN KEY({strKeyCols}) REFERENCES {tn} ({strPkKeyCols})";
        if(refC.CascadeDelete)
          fkSpec += " ON DELETE CASCADE";
        specs.Add(fkSpec);
      }
      //Build final Table statement
      var columnSpecs = string.Join("," + Environment.NewLine, specs);
      var script = $@"CREATE TABLE {table.FullName} (
{columnSpecs} 
); ";
      change.AddScript(DbScriptType.TableAdd, script);
    }

    public override void BuildColumnRenameSql(DbObjectChange change, DbColumnInfo oldColumn, DbColumnInfo newColumn) {
      //SQLite has no command to rename columns, so we use add/copy/drop method which creates new column, copies data and removes old one
      base.BuildColumnRenameSqlWithAddDrop(change, oldColumn, newColumn); 
    }

    protected override string GetColumnSpec(DbColumnInfo column, DbScriptOptions options = DbScriptOptions.None) {
      bool isComputed = column.ComputedKind != DbComputedKindExt.None;
      if (isComputed)
        return GetComputedColumnSpec(column, options);

      // See https://www.sqlite.org/autoinc.html
      // auto-inc columns is always INT64, but s
      if (column.Flags.IsSet(DbColumnFlags.Identity))
        return column.ColumnNameQuoted + " INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL"; //
      else 
       return base.GetColumnSpec(column, options);
    }

    private string GetComputedColumnSpec(DbColumnInfo column, DbScriptOptions options) {
      var typeStr = column.TypeInfo.DbTypeSpec;
      var virtStored = column.ComputedKind == DbComputedKindExt.Column ? "VIRTUAL" : "STORED";
      var spec =
        $"{column.ColumnNameQuoted} {typeStr} GENERATED ALWAYS AS ({column.ComputedAsExpression}) {virtStored}";
      return spec;
    }

  }
}
