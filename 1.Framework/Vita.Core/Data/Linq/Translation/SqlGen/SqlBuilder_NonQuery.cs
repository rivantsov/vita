using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;

namespace Vita.Data.Linq.Translation.SqlGen {
  partial class SqlBuilder {

    public SqlStatement BuildNonQuery(NonQueryLinqCommandData commandData) {
      switch(commandData.CommandType) {
        case LinqCommandType.Insert:
          return BuildInsertCommand(commandData);
        case LinqCommandType.Update:
          if(commandData.UseSimpleCommand)
            return BuildUpdateSimple(commandData);
          else
            return BuildUpdateWithSubquery(commandData);
        case LinqCommandType.Delete:
          if (commandData.UseSimpleCommand)
            return BuildDeleteSimple(commandData);
          else
            return BuildDeleteWithSubquery(commandData);

        default:
          Util.Throw("Linq command type {0} not supported.", commandData.CommandType);
          return null; 
      }
    }

    // Builds one-table update
    public SqlStatement BuildInsertCommand(NonQueryLinqCommandData command) {
      var template =
@"INSERT INTO {0} 
  ({1})
  {2}
";
      var selectSql = BuildSelectSql(command.BaseSelect).ToString();
      var colList = string.Join(", ", command.TargetColumns.Select(c => c.ColumnInfo.ColumnName.DoubleQuote()));
      var sql = string.Format(template, command.TargetTable.TableInfo.FullName, colList, selectSql);
      return new SqlStatement(sql);
    }

    // Builds one-table update
    public SqlStatement BuildUpdateSimple(NonQueryLinqCommandData command) {
      var setValueClauses = new List<string>();
      for(int i = 0; i < command.TargetColumns.Count; i++) {
        var col = command.TargetColumns[i];
        bool isPk = col.ColumnInfo.Flags.IsSet(DbColumnFlags.PrimaryKey);
        if(isPk)
          continue; //we ignore PK columns
        var outExpr = BuildExpression(command.SelectOutputValues[i]);
        //TODO: move this to sqlProvider
        var clause = string.Format("{0} = {1}", col.ColumnInfo.ColumnName.DoubleQuote(), outExpr);
        setValueClauses.Add(clause);
      }
      var setClause = string.Join(", ", setValueClauses);
      var sqlWhere = string.Empty;
      var whereList = command.BaseSelect.Where;
      if(whereList.Count > 0)
        sqlWhere = "\r\n  " + BuildWhere(new[] { command.TargetTable }, whereList);
      var sql = "UPDATE " + command.TargetTable.TableInfo.FullName + "\r\n SET " + setClause + sqlWhere + ";";
      return new SqlStatement(sql);
    } 

    public SqlStatement BuildUpdateWithSubquery(NonQueryLinqCommandData commandData) {
      Util.Check(this._dbModel.Driver.Supports(Driver.DbFeatures.UpdateFromSubQuery),
        "The database server does not support UPDATE statements with 'FROM <subquery>' clause, cannot translate this LINQ query.");
      var subQueryAlias = "_from";
      var setValueClauses = new List<string>();
      var whereClauses = new List<string>(); 
      for(int i = 0; i < commandData.TargetColumns.Count; i++) {
        var outExpr = commandData.SelectOutputValues[i] as SqlExpression;
        var col = commandData.TargetColumns[i];
        // change alias on PK columns - this is necessary to avoid ambiguous names. MS SQL does not like this
        bool isPk = col.ColumnInfo.Flags.IsSet(DbColumnFlags.PrimaryKey);
        if(isPk)
          outExpr.Alias += "_"; 
        var equalExpr = string.Format("{0} = {1}.{2}", col.ColumnInfo.ColumnName.DoubleQuote(), subQueryAlias, outExpr.Alias.DoubleQuote());
        if (isPk) 
          whereClauses.Add(equalExpr);
        else
          setValueClauses.Add(equalExpr);
      }
      var setClause = string.Join(", ", setValueClauses);
      var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : string.Empty;

      var fromClauseStmt = BuildSelect(commandData.BaseSelect);
      var fromSql = "\r\nFROM (\r\n" + fromClauseStmt + "\r\n     ) AS " + subQueryAlias;
      var sql = "UPDATE " + commandData.TargetTable.TableInfo.FullName + 
          "\r\n SET " + setClause + fromSql + "\r\n" + whereClause + ";";
      return new SqlStatement(sql);
    }

    public SqlStatement BuildDeleteSimple(NonQueryLinqCommandData commandData) {
      var sqlWhere = string.Empty;
      var whereList = commandData.BaseSelect.Where;
      if(whereList.Count > 0)
        sqlWhere = "\r\n  " + BuildWhere(new[] { commandData.TargetTable }, whereList);
      var tblInfo = commandData.TargetTable.TableInfo;
      var sql =string.Format("DELETE FROM {0} {1};", tblInfo.FullName, sqlWhere);
      return new SqlStatement(sql);
    }

    public SqlStatement BuildDeleteWithSubquery(NonQueryLinqCommandData commandData) {
      // base query must return set of PK values
      var targetEnt = commandData.TargetTable.TableInfo.Entity;
      Util.Check(targetEnt.PrimaryKey.ExpandedKeyMembers.Count == 1,
        "DELETE LINQ query with multiple tables cannot be used for table with composite primiry key. Entity: {0}", targetEnt.EntityType);
      var pk = targetEnt.PrimaryKey.ExpandedKeyMembers[0].Member;
      var outExpr = commandData.BaseSelect.Reader.Body;
      Util.Check(outExpr.Type == pk.DataType,
        "DELETE Query with multiple tables must return a value compatible with primary key of the table." +
        " Query returns: {0}; Primary key type: {1}, entity: {2}", outExpr.Type, pk.DataType, targetEnt.EntityType);
      var subQueryStmt = BuildSelect(commandData.BaseSelect);
      var subQuery = subQueryStmt.ToString();
      var pkCol = commandData.TargetTable.TableInfo.GetColumnByMemberName(pk.MemberName);
      Util.Check(pkCol != null, "Internal error in Linq engine: failed to find PK column for PK member {0}, entity {1}",
                pk.MemberName, targetEnt.EntityType);
      var strPkCol = _sqlProvider.GetColumn(pkCol.ColumnName);
      var sqlWhere = string.Format("   WHERE {0} IN (\r\n{1})", strPkCol, subQuery);
      var tblInfo = commandData.TargetTable.TableInfo;
      var sql = string.Format("DELETE FROM {0} {1};", tblInfo.FullName, sqlWhere);
      return new SqlStatement(sql);
    }

  }//class
}
