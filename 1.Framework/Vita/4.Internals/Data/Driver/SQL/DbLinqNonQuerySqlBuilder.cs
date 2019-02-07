using System;
using System.Collections.Generic;
using System.Linq;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Sql;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation;
using Vita.Data.Linq.Translation.Expressions;

namespace Vita.Data.Driver {

  public class DbLinqNonQuerySqlBuilder {
    protected DbModel DbModel;
    protected DbSqlDialect SqlDialect;
    protected NonQueryLinqCommand Command;
    protected DbLinqSqlBuilder LinqSqlBuilder; 


    public DbLinqNonQuerySqlBuilder(DbModel dbModel, NonQueryLinqCommand command) {
      DbModel = dbModel;
      this.Command = command;
      SqlDialect = DbModel.Driver.SqlDialect;
      LinqSqlBuilder = DbModel.Driver.CreateLinqSqlBuilder(dbModel, command.BaseLinqCommand);
    }

    public virtual SqlStatement BuildLinqNonQuerySql() {
      // check can use simple
      Command.UseSimpleCommand = CanUseSimpleNonQueryCommand(Command.Operation, Command.BaseSelect, Command.TargetTable);

      switch(Command.Operation) {
        case LinqOperation.Insert:
          return BuildLinqInsert();
        case LinqOperation.Update:
          if(Command.UseSimpleCommand)
            return BuildLinqUpdateSimple();
          else
            return BuildLinqUpdateWithSubquery();
        case LinqOperation.Delete:
          if(Command.UseSimpleCommand)
            return BuildLinqDelete();
          else
            return BuildLinqDeleteWithSubquery();

        default:
          Util.Throw("Linq command type {0} not supported.", Command.Operation);
          return null;
      }
    }

    protected bool CanUseSimpleNonQueryCommand(LinqOperation op, SelectExpression select, DbTableInfo targetTable) {
      switch(op) {
        case LinqOperation.Insert:
          return false;
        default:
          var allTables = select.Tables;
          var usesSkipTakeOrderBy = select.Offset != null || select.Limit != null;
          var useSimple = allTables.Count == 1 && allTables[0].TableInfo == targetTable && !usesSkipTakeOrderBy;
          return useSimple;
      }
    }

    // Builds one-table update
    public virtual SqlStatement BuildLinqInsert() {
      var tblName = Command.TargetTable.SqlFullName;
      var colParts = Command.TargetColumns.Select(c => c.SqlColumnNameQuoted).ToList();
      var colList = SqlFragment.CreateList(SqlTerms.Comma, colParts);
      var selectSql = LinqSqlBuilder.BuildSelectSql(Command.BaseSelect);
      var sqlInsert = this.SqlDialect.SqlLinqTemplateInsertFromSelect.Format(tblName, colList, selectSql);
      return CreateNonQueryStatement(sqlInsert, SqlKind.LinqInsert);
    }

    // Builds one-table update
    public virtual SqlStatement BuildLinqUpdateSimple() {
      var setValueClauses = new List<SqlFragment>();
      for(int i = 0; i < Command.TargetColumns.Count; i++) {
        var col = Command.TargetColumns[i];
        bool isPk = col.Flags.IsSet(DbColumnFlags.PrimaryKey);
        if(isPk)
          continue; //we ignore PK columns
        var colNamePart = col.SqlColumnNameQuoted;
        var outExprPart = LinqSqlBuilder.BuildLinqExpressionSql(Command.SelectOutputValues[i]);
        var clause = SqlDialect.SqlTemplateColumnAssignValue.Format(colNamePart, outExprPart);
        setValueClauses.Add(clause);
      }
      var setClause = SqlFragment.CreateList(SqlTerms.Comma, setValueClauses);
      var whereList = Command.BaseSelect.Where;
      SqlFragment sqlWhere = (whereList.Count > 0) ?
           LinqSqlBuilder.BuildWhereClause(Command.BaseSelect, whereList) :
           SqlTerms.Empty;
      var tablePart = Command.TargetTable.SqlFullName;
      var sqlUpdate = SqlDialect.SqlCrudTemplateUpdate.Format(tablePart, setClause, sqlWhere);
      return CreateNonQueryStatement(sqlUpdate, SqlKind.LinqUpdate);
    }

    private static SqlFragment _fromAlias = new TextSqlFragment("_from");

    public virtual SqlStatement BuildLinqUpdateWithSubquery() {
      Util.Check(DbModel.Driver.Supports(Data.Driver.DbFeatures.UpdateFromSubQuery),
         "The database server does not support UPDATE statements with 'FROM <subquery>' clause, cannot translate this LINQ query.");
      var setValueClauses = new List<SqlFragment>();
      var whereExprSqls = new List<SqlFragment>();
      for(int i = 0; i < Command.TargetColumns.Count; i++) {
        var outExpr = Command.SelectOutputValues[i] as SqlExpression;
        var col = Command.TargetColumns[i];
        // change alias on PK columns - this is necessary to avoid ambiguous names. MS SQL does not like this
        bool isPk = col.Flags.IsSet(DbColumnFlags.PrimaryKey);
        if(isPk)
          outExpr.Alias += "_";
        var colNamePart = col.SqlColumnNameQuoted;
        var outExprAliasPart = new TextSqlFragment(SqlDialect.QuoteName(outExpr.Alias));
        var equalExprSql = SqlDialect.SqlTemplateColumnAssignAliasValue.Format(colNamePart, _fromAlias, outExprAliasPart);
        if(isPk)
          whereExprSqls.Add(equalExprSql);
        else
          setValueClauses.Add(equalExprSql);
      }
      var setClause = SqlFragment.CreateList(SqlTerms.Comma, setValueClauses);
      SqlFragment whereClause = SqlTerms.Empty;
      if(whereExprSqls.Count > 0) {
        var whereCond = SqlFragment.CreateList(SqlTerms.And, whereExprSqls);
        whereClause = new CompositeSqlFragment(SqlTerms.Where, whereCond);
      }
      var fromClauseSql = LinqSqlBuilder.BuildSelectSql(Command.BaseSelect);
      var tableNameSql = Command.TargetTable.SqlFullName;
      var sqlUpdate = SqlDialect.SqlCrudTemplateUpdateFrom.Format(tableNameSql, setClause, fromClauseSql, _fromAlias, whereClause);
      return CreateNonQueryStatement(sqlUpdate, SqlKind.LinqUpdate);
    }

    public virtual SqlStatement BuildLinqDelete() {
      var whereList = Command.BaseSelect.Where;
      SqlFragment sqlWhere = SqlTerms.Empty;
      if(whereList.Count > 0)
        sqlWhere = LinqSqlBuilder.BuildWhereClause(Command.BaseSelect, whereList);
      var tblSql = Command.TargetTable.SqlFullName;
      var deleteSql = SqlDialect.SqlCrudTemplateDelete.Format(tblSql, sqlWhere);
      return CreateNonQueryStatement(deleteSql, SqlKind.LinqDelete);
    }

    public virtual SqlStatement BuildLinqDeleteWithSubquery() {
      // base query must return set of PK values
      var targetEnt = Command.TargetTable.Entity;
      Util.Check(targetEnt.PrimaryKey.ExpandedKeyMembers.Count == 1,
        "DELETE LINQ query with multiple tables may not be used for table with composite primiry key. Entity: {0}",
            targetEnt.EntityType);
      var pk = targetEnt.PrimaryKey.ExpandedKeyMembers[0].Member;
      var outExprType = Command.BaseSelect.ReaderOutputType;
      Util.Check(outExprType == pk.DataType,
        "DELETE Query with multiple tables must return a value compatible with primary key of the table." +
        " Query returns: {0}; Primary key type: {1}, entity: {2}", outExprType, pk.DataType, targetEnt.EntityType);
      // will throw if not found.
      var pkCol = Command.TargetTable.GetColumnByMemberName(pk.MemberName);
      var pkColSql = pkCol.SqlColumnNameQuoted;
      var tblNameSql = Command.TargetTable.SqlFullName;
      var subQuerySql = LinqSqlBuilder.BuildSelectSql(Command.BaseSelect);
      var deleteSql = SqlDialect.SqlCrudTemplateDeleteMany.Format(tblNameSql, pkColSql, subQuerySql);
      return CreateNonQueryStatement(deleteSql, SqlKind.LinqDelete);
    }

    private SqlStatement CreateNonQueryStatement(SqlFragment sql, SqlKind kind) {
      // Placeholders = null means that placeholders should be rediscovered
      return new SqlStatement(kind, sql, null, DbExecutionType.NonQuery, this.SqlDialect.PrecedenceHandler, QueryOptions.NoQueryCache);
    }

  } //class
}