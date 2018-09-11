using System;
using System.Collections.Generic;
using System.Linq; 
using System.Text;

using Vita.Data.Linq; 
using Vita.Data.Model;
using Vita.Data.SqlGen;
using Vita.Entities;
using Vita.Entities.Runtime;

namespace Vita.Data.Driver {

  partial class DbSqlBuilder {
    public readonly DbModel Model;
    public readonly DbDriver Driver;
    public DbSqlDialect SqlDialect;
    public QueryInfo QueryInfo; //might be null for CRUD commands (update,delete,insert)


    public DbSqlBuilder(DbModel dbModel, QueryInfo queryInfo) {
      Model = dbModel;
      Driver = Model.Driver;
      SqlDialect = Driver.SqlDialect;
      QueryInfo = queryInfo;
    }


    // CRUD methods 

    public virtual SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      // list of column names
      var insertCols = GetColumnsToInsert(table, record);
      var insertColsSqls = insertCols.Select(c => c.SqlColumnNameQuoted).ToList();
      var colListSql = SqlFragment.CreateList(SqlTerms.Comma, insertColsSqls);
      // values and placeholders
      var placeHolders = new List<SqlPlaceHolder>();
      var colValues = insertCols.Select(c => placeHolders.AddColumnValueRef(c)).ToArray();
      var valuesFragm = CompositeSqlFragment.Parenthesize(SqlFragment.CreateList(SqlTerms.Comma, colValues));
      // format SQL
      var sql = SqlDialect.SqlCrudTemplateInsert.Format(table.SqlFullName, colListSql, valuesFragm);
      var stmt = new SqlStatement(sql, placeHolders, DbExecutionType.NonQuery);
      return stmt;
    }

    public virtual SqlStatement BuildCrudUpdateOne(DbTableInfo table, EntityRecord rec, ISqlValueFormatter valueFormatter) {
      var placeHolders = new List<SqlPlaceHolder>();
      // find modified columns
      var setExprs = new List<SqlFragment>();
      foreach (var col in table.UpdatableColumns) {
        var newValue = rec.ValuesModified[col.Member.ValueIndex];
        if (newValue == null)
          continue;
        var valueSql = valueFormatter.FormatValue(rec, col, SqlFormatOptions.PreferLiteral);
        setExprs.Add(new CompositeSqlFragment(col.SqlColumnNameQuoted, SqlTerms.Equal, valueSql));
      }
      var setList = SqlFragment.CreateList(SqlTerms.Comma, setExprs);
      var whereCond = BuildWhereConditonForPrimaryKey(table, placeHolders);
      var whereClause = new CompositeSqlFragment(SqlTerms.Where, whereCond);
      var sql = SqlDialect.SqlCrudTemplateUpdate.Format(table.SqlFullName, setList, whereClause);
      var stmt = new SqlStatement(sql, placeHolders, DbExecutionType.NonQuery, 
                         SqlDialect.PrecedenceHandler, QueryOptions.NoQueryCache);
      return stmt;
    }

    public virtual SqlStatement BuildCrudDeleteMany(DbTableInfo table, IList<EntityRecord> records) {
      var pk = table.PrimaryKey;
      Util.Check(pk.KeyColumns.Count == 1, "Fatal: cannot use DeleteMany for table with composite PK.");
      var pkCol = pk.KeyColumns[0].Column;
      var placeHolders = new List<SqlPlaceHolder>();
      // TODO: refactor DataCommandBuilder and method of assigning param values!!!
      // Temp solution. TypeRegistry.GetListLiteral() requires typed array
      var idArray = Array.CreateInstance(pkCol.Member.DataType, records.Count);
      for(int i = 0; i < records.Count; i++) {
        var idValue = records[i].GetValueDirect(pkCol.Member);
        idArray.SetValue(idValue, i);
      }
      // the last parameter is value reader, we return literal array
      var pkListPrm = new SqlArrayValuePlaceHolder(pkCol.Member.DataType, (x => idArray));
      placeHolders.Add(pkListPrm); 
      var sql = SqlDialect.SqlCrudTemplateDeleteMany.Format(table.SqlFullName, pkCol.SqlColumnNameQuoted, pkListPrm);
      var stmt = new SqlStatement(sql, placeHolders, DbExecutionType.NonQuery);
      return stmt;
    }

    public virtual SqlStatement BuildCrudDeleteOne(DbTableInfo table) {
      var placeHolders = new List<SqlPlaceHolder>();
      var whereCond = BuildWhereConditonForPrimaryKey(table, placeHolders);
      var whereClause = new CompositeSqlFragment(SqlTerms.Where, whereCond);
      var sql = SqlDialect.SqlCrudTemplateDelete.Format(table.SqlFullName, whereClause);
      var stmt = new SqlStatement(sql, placeHolders, DbExecutionType.NonQuery);
      return stmt;
    }

    public virtual SqlStatement BuildCrudInsertMany(DbTableInfo table, IList<EntityRecord> records, ISqlValueFormatter formatter) {
      // list of column names
      var insertCols = GetColumnsToInsert(table, records);
      var insertColsSqls = insertCols.Select(c => c.SqlColumnNameQuoted).ToList();
      var colListSql = SqlFragment.CreateList(SqlTerms.Comma, insertColsSqls);
      //values set
      var rowFragments = new List<SqlFragment>();
      foreach (var rec in records) {
        var colValues = insertCols.Select(c => formatter.FormatValue(rec, c, SqlFormatOptions.PreferLiteral)).ToArray();
        var valuesFragm = CompositeSqlFragment.Parenthesize(SqlFragment.CreateList(SqlTerms.Comma, colValues));
        rowFragments.Add(valuesFragm);
      }
      var valuesFragment = SqlFragment.CreateList(SqlTerms.CommaNewLineIndent, rowFragments);
      var sql = SqlDialect.SqlCrudTemplateInsert.Format(table.SqlFullName, colListSql, valuesFragment);
      var stmt = new SqlStatement(sql, null, DbExecutionType.NonQuery, null, QueryOptions.NoQueryCache);
      return stmt;
    }

    // -------------- Helper methods -------------------------------------------------------
    protected SqlFragment BuildWhereConditonForPrimaryKey(DbTableInfo table, IList<SqlPlaceHolder> placeHolders) {
      var pkCols = table.PrimaryKey.KeyColumns;
      // short way for one-column PK
      if (pkCols.Count == 1) {
        var pkCol = pkCols[0];
        var colValue = placeHolders.AddColumnValueRef(pkCol.Column);
        return new CompositeSqlFragment(pkCol.Column.SqlColumnNameQuoted, SqlTerms.Equal, colValue);
      }
      //general case
      var conds = new List<SqlFragment>();
      foreach (var pkCol in pkCols) {
        var colValue = placeHolders.AddColumnValueRef(pkCol.Column);
        conds.Add(new CompositeSqlFragment(pkCol.Column.SqlColumnNameQuoted, SqlTerms.Equal, colValue));
      }
      return SqlFragment.CreateList(SqlTerms.And, conds);
    }

    // TODO: make it return only assigned columns, or those that have values different from default
    // this requires implementing bit masks
    public IList<DbColumnInfo> GetColumnsToInsert(DbTableInfo table, EntityRecord record) {
      return table.InsertColumns;
    }
    public IList<DbColumnInfo> GetColumnsToInsert(DbTableInfo table, IList<EntityRecord> record) {
      return table.InsertColumns;
    }

  }//class
}
