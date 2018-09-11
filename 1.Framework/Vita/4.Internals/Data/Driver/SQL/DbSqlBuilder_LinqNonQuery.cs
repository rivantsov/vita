using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Linq.Translation;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.SqlGen;
using Vita.Data.Linq;
using Vita.Entities.Runtime;
using System.Linq.Expressions;

namespace Vita.Data.Driver {

  public partial class DbSqlBuilder {

    public SqlStatement BuildLinqNonQuery(NonQueryLinqCommandData commandData) {
      switch(commandData.Operation) {
        case EntityOperation.Insert:
          return BuildLinqInsert(commandData);
        case EntityOperation.Update:
          if(commandData.UseSimpleCommand)
            return BuildLinqUpdateSimple(commandData);
          else
            return BuildLinqUpdateWithSubquery(commandData);
        case EntityOperation.Delete:
          if(commandData.UseSimpleCommand)
            return BuildLinqDelete(commandData);
          else
            return BuildLinqDeleteWithSubquery(commandData);

        default:
          Util.Throw("Linq command type {0} not supported.", commandData.Operation);
          return null;
      }
    }

    // Builds one-table update
    public SqlStatement BuildLinqInsert(NonQueryLinqCommandData command) {
      var tblName = command.TargetTable.TableInfo.SqlFullName;
      var colParts = command.TargetColumns.Select(c => c.ColumnInfo.SqlColumnNameQuoted).ToList();
      var colList = SqlFragment.CreateList(SqlTerms.Comma, colParts);
      var selectSql = BuildSelect(command.BaseSelect);
      var sqlInsert = this.SqlDialect.SqlLinqTemplateInsertFromSelect.Format(tblName, colList, selectSql);
      return SqlStatement.CreateLinqNonQuery(sqlInsert, SqlDialect.PrecedenceHandler);
    }

    // Builds one-table update
    public SqlStatement BuildLinqUpdateSimple(NonQueryLinqCommandData command) {
      var setValueClauses = new List<SqlFragment>();
      for(int i = 0; i < command.TargetColumns.Count; i++) {
        var col = command.TargetColumns[i];
        bool isPk = col.ColumnInfo.Flags.IsSet(DbColumnFlags.PrimaryKey);
        if(isPk)
          continue; //we ignore PK columns
        var colNamePart = col.ColumnInfo.SqlColumnNameQuoted;
        var outExprPart = BuildLinqExpressionSql(command.SelectOutputValues[i]);
        var clause = SqlDialect.SqlTemplateColumnAssignValue.Format(colNamePart, outExprPart);
        setValueClauses.Add(clause);
      }
      var setClause = SqlFragment.CreateList(SqlTerms.Comma, setValueClauses);
      var whereList = command.BaseSelect.Where;
      SqlFragment sqlWhere = (whereList.Count > 0) ? 
           BuildWhereClause(command.BaseSelect, new[] { command.TargetTable }, whereList) : 
           SqlTerms.Empty;
      var tablePart = command.TargetTable.TableInfo.SqlFullName;
      var sqlUpdate = SqlDialect.SqlCrudTemplateUpdate.Format(tablePart, setClause, sqlWhere);
      return SqlStatement.CreateLinqNonQuery(sqlUpdate, SqlDialect.PrecedenceHandler);
    }

    private static SqlFragment _fromAlias = new TextSqlFragment("_from");

    public SqlStatement BuildLinqUpdateWithSubquery(NonQueryLinqCommandData commandData) {
      Util.Check(Driver.Supports(Data.Driver.DbFeatures.UpdateFromSubQuery),
         "The database server does not support UPDATE statements with 'FROM <subquery>' clause, cannot translate this LINQ query.");
      var setValueClauses = new List<SqlFragment>();
      var whereExprSqls = new List<SqlFragment>();
      for(int i = 0; i < commandData.TargetColumns.Count; i++) {
        var outExpr = commandData.SelectOutputValues[i] as SqlExpression;
        var col = commandData.TargetColumns[i];
        // change alias on PK columns - this is necessary to avoid ambiguous names. MS SQL does not like this
        bool isPk = col.ColumnInfo.Flags.IsSet(DbColumnFlags.PrimaryKey);
        if(isPk)
          outExpr.Alias += "_";
        var colNamePart = col.ColumnInfo.SqlColumnNameQuoted;
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
      var fromClauseSql = BuildSelect(commandData.BaseSelect);
      var tableNameSql = commandData.TargetTable.TableInfo.SqlFullName;
      var sqlUpdate = SqlDialect.SqlCrudTemplateUpdateFrom.Format(tableNameSql, setClause, fromClauseSql, _fromAlias, whereClause);
      return SqlStatement.CreateLinqNonQuery(sqlUpdate, SqlDialect.PrecedenceHandler);
    }


    public SqlStatement BuildLinqDelete(NonQueryLinqCommandData commandData) {
      var whereList = commandData.BaseSelect.Where;
      SqlFragment sqlWhere = SqlTerms.Empty; 
      if (whereList.Count > 0)
        sqlWhere = this.BuildWhereClause(commandData.BaseSelect, new[] { commandData.TargetTable }, whereList);
      var tblSql = commandData.TargetTable.TableInfo.SqlFullName;
      var deleteSql = SqlDialect.SqlCrudTemplateDelete.Format(tblSql, sqlWhere);
      return SqlStatement.CreateLinqNonQuery(deleteSql, SqlDialect.PrecedenceHandler);
    }


    public SqlStatement BuildLinqDeleteWithSubquery(NonQueryLinqCommandData commandData) {
      // base query must return set of PK values
      var targetEnt = commandData.TargetTable.TableInfo.Entity;
      Util.Check(targetEnt.PrimaryKey.ExpandedKeyMembers.Count == 1,
        "DELETE LINQ query with multiple tables may not be used for table with composite primiry key. Entity: {0}", 
            targetEnt.EntityType);
      var pk = targetEnt.PrimaryKey.ExpandedKeyMembers[0].Member;
      var outExprType = commandData.BaseSelect.ReaderOutputType;
      Util.Check(outExprType == pk.DataType,
        "DELETE Query with multiple tables must return a value compatible with primary key of the table." +
        " Query returns: {0}; Primary key type: {1}, entity: {2}", outExprType, pk.DataType, targetEnt.EntityType);
      // will throw if not found.
      var pkCol = commandData.TargetTable.TableInfo.GetColumnByMemberName(pk.MemberName);
      var pkColSql = pkCol.SqlColumnNameQuoted;
      var tblNameSql = commandData.TargetTable.TableInfo.SqlFullName;
      var subQuerySql = BuildSelect(commandData.BaseSelect);
      var deleteSql = SqlDialect.SqlCrudTemplateDeleteMany.Format(tblNameSql, pkColSql, subQuerySql);
      return SqlStatement.CreateLinqNonQuery(deleteSql, SqlDialect.PrecedenceHandler);
    }

  } //class
}