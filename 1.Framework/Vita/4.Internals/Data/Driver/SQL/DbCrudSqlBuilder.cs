using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Data.Runtime;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Driver {

  public class DbCrudSqlBuilder {
    protected DbModel DbModel;
    protected DbSqlDialect SqlDialect;

    public DbCrudSqlBuilder(DbModel dbModel) {
      DbModel = dbModel;
      SqlDialect = DbModel.Driver.SqlDialect;
    }

    public virtual SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      // list of column names
      var insertCols = GetColumnsToInsert(table, record);
      var insertColsSqls = insertCols.Select(c => c.SqlColumnNameQuoted).ToList();
      var colListSql = SqlFragment.CreateList(SqlTerms.Comma, insertColsSqls);
      // values and placeholders
      var placeHolders = new SqlPlaceHolderList();
      var colSqls = new List<SqlFragment>();
      foreach(var insertCol in insertCols) {
        var ph = new SqlColumnValuePlaceHolder(insertCol);
        placeHolders.Add(ph);
        colSqls.Add(ph);
      }
      var valuesFragm = CompositeSqlFragment.Parenthesize(SqlFragment.CreateList(SqlTerms.Comma, colSqls));
      // format SQL
      var sql = SqlDialect.SqlCrudTemplateInsert.Format(table.SqlFullName, colListSql, valuesFragm);
      var stmt = new SqlStatement(SqlKind.InsertOne, sql, placeHolders, DbExecutionType.NonQuery);
      return stmt;
    }

    public virtual SqlStatement BuildCrudUpdateOne(DbTableInfo table, EntityRecord rec) {
      var placeHolders = new SqlPlaceHolderList();
      // find modified columns
      var setExprs = new List<SqlFragment>();
      foreach(var col in table.UpdatableColumns) {
        if(!rec.IsValueChanged(col.Member))
          continue;
        var valueSql = new SqlColumnValuePlaceHolder(col);
        placeHolders.Add(valueSql);
        setExprs.Add(new CompositeSqlFragment(col.SqlColumnNameQuoted, SqlTerms.Equal, valueSql));
      }
      var setList = SqlFragment.CreateList(SqlTerms.Comma, setExprs);
      var whereCond = BuildWhereConditonForUpdateDeleteOne(table, placeHolders);
      var whereClause = new CompositeSqlFragment(SqlTerms.Where, whereCond);
      var sql = SqlDialect.SqlCrudTemplateUpdate.Format(table.SqlFullName, setList, whereClause);
      var stmt = new SqlStatement(SqlKind.UpdateOne, sql, placeHolders, DbExecutionType.NonQuery,
                         SqlDialect.PrecedenceHandler, QueryOptions.NoQueryCache);
      return stmt;
    }

    public virtual SqlStatement BuildCrudDeleteMany(DbTableInfo table) {
      var pk = table.PrimaryKey;
      Util.Check(pk.KeyColumns.Count == 1, "Fatal: cannot use DeleteMany for table with composite PK.");
      var pkCol = pk.KeyColumns[0].Column;
      var elemTypeDef = pkCol.TypeInfo.TypeDef;
      var pkListPh = new SqlListParamPlaceHolder(pkCol.Member.DataType, elemTypeDef,
                   // Placeholder expects here function that reads the value from local environment;
                   // we provide a function that will take List<EntityRecord> and produce list of PK values
                   valueReader: recs => GetPrimaryKeyValues(recs, table),
                   // ToLiteral
                   formatLiteral: list => DbModel.Driver.SqlDialect.ListToLiteral(list, elemTypeDef)
                   );
      var placeHolders = new SqlPlaceHolderList();
      placeHolders.Add(pkListPh);
      var sql = SqlDialect.SqlCrudTemplateDeleteMany.Format(table.SqlFullName, pkCol.SqlColumnNameQuoted, pkListPh);
      var stmt = new SqlStatement(SqlKind.DeleteMany, sql, placeHolders, DbExecutionType.NonQuery);
      return stmt;
    }

    public virtual SqlStatement BuildCrudDeleteOne(DbTableInfo table) {
      var placeHolders = new SqlPlaceHolderList();
      var whereCond = BuildWhereConditonForUpdateDeleteOne(table, placeHolders);
      var whereClause = new CompositeSqlFragment(SqlTerms.Where, whereCond);
      var sql = SqlDialect.SqlCrudTemplateDelete.Format(table.SqlFullName, whereClause);
      var stmt = new SqlStatement(SqlKind.DeleteOne, sql, placeHolders, DbExecutionType.NonQuery);
      return stmt;
    }

    // InsertMany will never be reused, so we prefer to use literals, not parameters
    public virtual SqlStatement BuildCrudInsertMany(DbTableInfo table, IList<EntityRecord> records, IColumnValueFormatter formatter) {
      // list of column names
      var insertColPhs = GetColumnPlaceholdersForInsert(table, records);
      var insertColsSqls = insertColPhs.Select(ph => ph.Column.SqlColumnNameQuoted).ToList();
      var colListSql = SqlFragment.CreateList(SqlTerms.Comma, insertColsSqls);
      //values set
      var rowFragments = new List<SqlFragment>();
      var colValueSqls = new List<SqlFragment>();
      foreach(var rec in records) {
        colValueSqls.Clear();
        foreach(var colPh in insertColPhs)
          colValueSqls.Add(new TextSqlFragment(formatter.FormatColumnValuePlaceHolder(colPh, rec)));
        var valuesRow = CompositeSqlFragment.Parenthesize(SqlFragment.CreateList(SqlTerms.Comma, colValueSqls));
        rowFragments.Add(valuesRow);
      }
      var allValuesSql = SqlFragment.CreateList(SqlTerms.CommaNewLineIndent, rowFragments);
      var sql = SqlDialect.SqlCrudTemplateInsert.Format(table.SqlFullName, colListSql, allValuesSql);
      var stmt = new SqlStatement(SqlKind.InsertMany, sql, null, DbExecutionType.NonQuery, null, QueryOptions.NoQueryCache);
      return stmt;
    }

    // -------------- Helper methods -------------------------------------------------------


    // The resulting list is sent as array to db server (as literal or parameter)
    // For parameter case, it is important for some servers (Pgres) that list is a typed list, object[] does not work
    // So we create an array of specific type
    protected IList GetPrimaryKeyValues(object[] input, DbTableInfo table) {
      var records = (IList<EntityRecord>)input[0];
      var pkCol = table.PrimaryKey.KeyColumns[0].Column;
      var idArray = Array.CreateInstance(pkCol.Member.DataType, records.Count);
      for(int i = 0; i < records.Count; i++)
        idArray.SetValue(records[i].GetValueDirect(pkCol.Member), i);
      return idArray;
    }

    protected SqlFragment BuildWhereConditonForUpdateDeleteOne(DbTableInfo table, SqlPlaceHolderList placeHolders) {
      var pkCols = table.PrimaryKey.KeyColumns;
      var hasRowVersion = table.Entity.Flags.HasFlag(EntityFlags.HasRowVersion);
      SqlPlaceHolder colPh;
      // short way for one-column PK
      if(pkCols.Count == 1 && !hasRowVersion) {
        var pkCol = pkCols[0].Column;
        colPh = new SqlColumnValuePlaceHolder(pkCol);
        placeHolders.Add(colPh);
        return new CompositeSqlFragment(pkCol.SqlColumnNameQuoted, SqlTerms.Equal, colPh);
      }
      //general case: 
      //  add row version to column list if there's row version. We must compare row version in WHERE clause
      var allCols = pkCols.Select(kc => kc.Column).ToList();
      if(hasRowVersion) {
        var rvCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.RowVersion));
        allCols.Add(rvCol);
      }
      var conds = new List<SqlFragment>();
      foreach(var col in allCols) {
        var colSql = new SqlColumnValuePlaceHolder(col);
        placeHolders.Add(colSql);
        conds.Add(new CompositeSqlFragment(col.SqlColumnNameQuoted, SqlTerms.Equal, colSql));
      }
      return SqlFragment.CreateList(SqlTerms.And, conds);
    }

    // TODO: make it return only assigned columns, or those that have values different from default
    public IList<DbColumnInfo> GetColumnsToInsert(DbTableInfo table, EntityRecord record) {
      return table.InsertColumns;
    }

    public IList<SqlColumnValuePlaceHolder> GetColumnPlaceholdersForInsert(DbTableInfo table, IList<EntityRecord> record) {
      // TODO: refactor, cache it in DbColumnInfo
      return table.InsertColumns.Select(c => new SqlColumnValuePlaceHolder(c)).ToList();
    }

  }//class
}
