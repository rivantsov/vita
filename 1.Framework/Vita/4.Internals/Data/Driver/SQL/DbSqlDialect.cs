using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using Vita.Data.Driver.TypeSystem;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Data.Driver {

  public partial class DbSqlDialect {
    DbDriver _driver; 

    public int MaxLiteralLength = 100;
    public int MaxRecordsInInsertMany = 200; 

    public string DynamicSqlParameterPrefix = "p";
    public string LeftSafeQuote = "\"";
    public string RightSafeQuote = "\"";
    public string DDLSeparator = Environment.NewLine;
    public char LikeEscapeChar = '\\';
    public char[] LikeWildCardChars = new char[] { '_', '%', '[', ']', '\\' };


    public SqlFragment SqlCountStar = new TextSqlFragment("COUNT(*)");
    public SqlFragment SqlNullAsEmpty = new TextSqlFragment("NULL AS \"EMPTY\"");
    public SqlFragment SqlFakeOrderByClause = new TextSqlFragment("ORDER BY (SELECT 1)");

    public SqlTemplate SqlTemplateConcatMany = new SqlTemplate("CONCAT({0})");
    public TextSqlFragment SqlConcatListDelimiter = new TextSqlFragment(", "); 

    public SqlTemplate SqlTemplateColumnAssignValue = new SqlTemplate("{0} = {1}");
    public SqlTemplate SqlTemplateColumnAssignAliasValue = new SqlTemplate("{0} = {1}.{2}");
    public SqlTemplate SqlTemplateOrderBy = new SqlTemplate("ORDER BY {0}");
    public SqlTemplate OffsetTemplate = new SqlTemplate("OFFSET {0} ROWS");
    public SqlTemplate OffsetLimitTemplate = new SqlTemplate("OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY");

    public TextSqlFragment BatchBegin = SqlTerms.Empty;
    public TextSqlFragment BatchEnd = SqlTerms.Empty;
    public TextSqlFragment BatchBeginTransaction = new TextSqlFragment("BEGIN TRANSACTION;\r\n");
    public TextSqlFragment BatchCommitTransaction = new TextSqlFragment("COMMIT TRANSACTION;\r\n");

    public int MaxParamCount = 2000;
    protected Dictionary<ExpressionType, SqlTemplate> StandardExpressionTemplates = new Dictionary<ExpressionType, SqlTemplate>();
    protected Dictionary<SqlFunctionType, SqlTemplate> SqlFunctionTemplates = new Dictionary<SqlFunctionType, SqlTemplate>();
    protected Dictionary<AggregateType, SqlTemplate> AggregateTemplates = new Dictionary<AggregateType, SqlTemplate>();

    public SqlPrecedenceHandler PrecedenceHandler; 

    public DbSqlDialect(DbDriver driver, SqlPrecedenceHandler precedenceHandler = null) {
      _driver = driver; 
      PrecedenceHandler = precedenceHandler ?? new SqlPrecedenceHandler();
      InitTemplates();
    }
    // Linq Non-query templates
    //important - semicolon at the end should be just like that, no spaces or new-line around; we strip it to add id-return clause 
    public SqlTemplate SqlCrudTemplateInsert = new SqlTemplate(
@"INSERT INTO {0} 
    ({1})
    VALUES 
    {2};"); 

    public SqlTemplate SqlLinqTemplateInsertFromSelect = new SqlTemplate(
@"INSERT INTO {0} 
    ({1})
    {2};");

    public SqlTemplate SqlCrudTemplateUpdate = new SqlTemplate(
@"UPDATE {0}
    SET {1}
    {2};");

    public SqlTemplate SqlCrudTemplateUpdateFrom = new SqlTemplate(
 @"UPDATE {0} 
  SET {1}
  FROM ({2}) {3} 
  {4};");

    public SqlTemplate SqlCrudTemplateDelete = new SqlTemplate(
@"DELETE FROM {0} 
    {1};");

    public SqlTemplate SqlCrudTemplateDeleteMany = new SqlTemplate(
@"DELETE FROM {0} 
    WHERE {1} IN ({2})");


    // ====================================================================================================
    public virtual SqlTemplate GetExpressionTemplate(Expression expression) {
      return GetTemplate(expression.NodeType);
    }

    public virtual SqlTemplate GetSqlFunctionTemplate(SqlFunctionExpression expr) {
      return GetTemplate(expr.FunctionType); 
    }

    public virtual SqlTemplate GetAggregateTemplate(AggregateExpression expr) {
      return GetTemplate(expr.AggregateType);
    }

    public virtual SqlTemplate GetTemplate(SqlFunctionType functionType) {
      if(this.SqlFunctionTemplates.TryGetValue(functionType, out SqlTemplate template))
        return template;
      return null;
    }
    public virtual SqlTemplate GetTemplate(ExpressionType nodeType) {
      if(StandardExpressionTemplates.TryGetValue(nodeType, out SqlTemplate template))
        return template;
      return null;
    }
    public virtual SqlTemplate GetTemplate(AggregateType aggregateType) {
      if(AggregateTemplates.TryGetValue(aggregateType, out SqlTemplate template))
        return template;
      return null;
    }

    public virtual void InitTemplates() {
      // build standard templates 
      // arithmetic
      AddTemplate("{0} = {1}", ExpressionType.Equal);
      AddTemplate("{0} <> {1}", ExpressionType.NotEqual);
      AddTemplate("{0} > {1}", ExpressionType.GreaterThan);
      AddTemplate("{0} >= {1}", ExpressionType.GreaterThanOrEqual);
      AddTemplate("{0} < {1}", ExpressionType.LessThan);
      AddTemplate("{0} <= {1}", ExpressionType.LessThanOrEqual);
      AddTemplate("{0} + {1}", ExpressionType.Add, ExpressionType.AddChecked);
      AddTemplate("{0} - {1}", ExpressionType.Subtract, ExpressionType.SubtractChecked);
      AddTemplate("{0} * {1}", ExpressionType.Multiply, ExpressionType.MultiplyChecked);
      AddTemplate("{0} / {1}", ExpressionType.Divide);
      AddTemplate("{0} % {1}", ExpressionType.Modulo);
      AddTemplate("-{0}", ExpressionType.Negate, ExpressionType.NegateChecked);
      AddTemplate("+{0}", ExpressionType.UnaryPlus);
      //bool 
      AddTemplate("NOT {0}", ExpressionType.Not);
      AddTemplate("{0} AND {1}", ExpressionType.And, ExpressionType.AndAlso);
      AddTemplate("{0} OR {1}", ExpressionType.Or, ExpressionType.OrElse);

      //misc
      AddTemplate("COALESCE({0}, {1})", ExpressionType.Coalesce);
      AddTemplate("IIF({0}, {1}, {2})", ExpressionType.Conditional);

      // SqlFunction types
      AddTemplate("{0} IS NULL", SqlFunctionType.IsNull);
      AddTemplate("{0} IS NOT NULL", SqlFunctionType.IsNotNull);
      AddTemplate("({0} = {1} OR ({0} IS NULL) AND ({1} IS NULL))", SqlFunctionType.EqualNullables);
      AddTemplate(@"EXISTS 
  {0}", SqlFunctionType.Exists);
      var strLike = @"{0} LIKE {1} ESCAPE '\'";
      AddTemplate(strLike, SqlFunctionType.Like);
      AddTemplate("{0} = {1}", SqlFunctionType.StringEqual);
      AddTemplate("{0} IN ({1})", SqlFunctionType.In, SqlFunctionType.InArray);
      AddTemplate("{0} & {1}", SqlFunctionType.AndBitwise);
      AddTemplate("{0} | {1}", SqlFunctionType.OrBitwise);
      AddTemplate("{0} ^ {1}", SqlFunctionType.XorBitwise);

      AddTemplate("ABS({0})", SqlFunctionType.Abs);
      AddTemplate("EXP({0})", SqlFunctionType.Exp);
      AddTemplate("FLOOR({0})", SqlFunctionType.Floor);
      AddTemplate("LN({0})", SqlFunctionType.Ln);
      AddTemplate("LOG({0})", SqlFunctionType.Log);
      AddTemplate("ROUND({0})", SqlFunctionType.Round);
      AddTemplate("SIGN({0})", SqlFunctionType.Sign);
      AddTemplate("SQRT({0})", SqlFunctionType.Sqrt);

      //Aggregates
      AddTemplate("COUNT({0})", AggregateType.Count);
      AddTemplate("MIN({0})", AggregateType.Min);
      AddTemplate("MAX({0})", AggregateType.Max);
      AddTemplate("SUM({0})", AggregateType.Sum);
      AddTemplate("AVG({0})", AggregateType.Average);

    }

    // standard expression templates
    protected SqlTemplate AddTemplate(string template, params ExpressionType[] types) {
      var prec = PrecedenceHandler.GetPrecedence(types[0]); //we assume prec is the same for all types
      var sqlTemplate = new SqlTemplate(template, prec);
      foreach(var type in types)
        StandardExpressionTemplates[type] = sqlTemplate;
      return sqlTemplate;
    }

    // Sql Function templates
    protected SqlTemplate AddTemplate(string template, params SqlFunctionType[] types) {
      var prec = PrecedenceHandler.GetPrecedence(types[0]); //we assume prec is the same for all types
      var sqlTemplate = new SqlTemplate(template, prec);
      foreach(var type in types)
        SqlFunctionTemplates[type] = sqlTemplate;
      return sqlTemplate;
    }

    protected SqlTemplate AddTemplate(string template, AggregateType aggregateType) {
      var sqlTemplate = new SqlTemplate(template);
      AggregateTemplates[aggregateType] = sqlTemplate;
      return sqlTemplate;
    }

    public string QuoteName(string name) {
      return LeftSafeQuote + name + RightSafeQuote;
    }

    public virtual string FormatFullName(string schema, string name) {
      if(this._driver.Supports(DbFeatures.Schemas))
        return LeftSafeQuote + schema + RightSafeQuote + "." + LeftSafeQuote + name + RightSafeQuote;
      else
        return LeftSafeQuote + name + RightSafeQuote;
    }

    public virtual Type GetAggregateResultType(AggregateType aggregateType, Type[] opTypes) {
      switch(aggregateType) {
        case AggregateType.Count:
          return typeof(int);

        case AggregateType.Average:
        case AggregateType.Max:
        case AggregateType.Min:
        case AggregateType.Sum:
          return opTypes[0];
        default:
          return opTypes[0];
      }
    }

    public virtual bool IsSqlTier(Expression expression, LinqCommand command) {
      var sqlExpr = expression as SqlExpression;
      if(sqlExpr != null) {
        switch(sqlExpr.SqlNodeType) {
          case SqlExpressionType.Select:
          case SqlExpressionType.Column:
          case SqlExpressionType.Table:
          case SqlExpressionType.ExternalValue:
          case SqlExpressionType.SqlFunction:
            return true;
          case SqlExpressionType.Group:
          case SqlExpressionType.DerivedTable:
            return false;
          default:
            return true;
        }
      }
      switch(expression.NodeType) {
        case ExpressionType.ArrayLength:
        case ExpressionType.ArrayIndex:
        case ExpressionType.Call:
        case ExpressionType.Convert:
        case ExpressionType.ConvertChecked:
        case ExpressionType.Invoke:
        case ExpressionType.Lambda:
        case ExpressionType.ListInit:
        case ExpressionType.MemberAccess:
        case ExpressionType.MemberInit:
        case ExpressionType.New:
        case ExpressionType.NewArrayInit:
        case ExpressionType.NewArrayBounds:
        case ExpressionType.Parameter:
        case ExpressionType.SubtractChecked:
        case ExpressionType.TypeAs:
        case ExpressionType.TypeIs:
          return false;
        default:
          return true;
      }
    }

    public virtual Type GetSqlFunctionResultType(SqlFunctionType functionType, Type[] operandTypes) {
      Type defaultType = null;
      //RI: changed to use op[1] (from 0) here - this is selector
      if(operandTypes != null && operandTypes.Length > 0)
        defaultType = operandTypes[operandTypes.Length - 1];
      switch(functionType) {
        case SqlFunctionType.IsNull:
        case SqlFunctionType.IsNotNull:
        case SqlFunctionType.EqualNullables:
        case SqlFunctionType.StringEqual:
          return typeof(bool);
        case SqlFunctionType.Concat:
          return typeof(string);
        case SqlFunctionType.Exists:
          return typeof(bool);
        case SqlFunctionType.Like:
          return typeof(bool);
        case SqlFunctionType.StringLength:
          return typeof(int);
        case SqlFunctionType.ToUpper:
        case SqlFunctionType.ToLower:
          return typeof(string);
        case SqlFunctionType.In:
        case SqlFunctionType.InArray:
          return typeof(bool);
        case SqlFunctionType.Substring:
          return defaultType;
        case SqlFunctionType.Trim:
        case SqlFunctionType.LTrim:
        case SqlFunctionType.RTrim:
          return typeof(string);
        case SqlFunctionType.Year:
        case SqlFunctionType.Month:
        case SqlFunctionType.Day:
        case SqlFunctionType.Hour:
        case SqlFunctionType.Second:
        case SqlFunctionType.Minute:
        case SqlFunctionType.Millisecond:
        case SqlFunctionType.Week:
          return typeof(int);
        case SqlFunctionType.Now:
        case SqlFunctionType.Date:
          return typeof(DateTime);
        case SqlFunctionType.Time:
          return typeof(TimeSpan);
        case SqlFunctionType.DateDiffInMilliseconds:
          return typeof(long);
        case SqlFunctionType.Abs:
        case SqlFunctionType.Exp:
        case SqlFunctionType.Floor:
        case SqlFunctionType.Ln:
        case SqlFunctionType.Log:
        case SqlFunctionType.Round:
        case SqlFunctionType.Sign:
        case SqlFunctionType.Sqrt:
          return defaultType;
        case SqlFunctionType.AndBitwise:
        case SqlFunctionType.OrBitwise:
        case SqlFunctionType.XorBitwise:
          return defaultType;
        case SqlFunctionType.ConvertBoolToBit:
          return typeof(bool);

        default:
          Util.Throw("Unknown SqlFunctionType value {0}", functionType);
          return null;
      }
    }

    public virtual DbTableFilter BuildDbTableFilter(DbTableInfo tbl, EntityFilter entityFilter, ILog log = null) {
      var dbFilter = new DbTableFilter() { EntityFilter = entityFilter };
      var colList = new List<DbColumnInfo>();
      // setup columns
      var filterTempl = entityFilter.Template;
      foreach(var mName in filterTempl.ArgNames) {
        var col = tbl.GetColumnByMemberName(mName);
        if(col == null)
          col = tbl.Columns.FindByName(mName);
        if(col != null)
          colList.Add(col);
        else {
          if(log == null)
            Util.Throw($"Index/key filter on entity {tbl.Entity.Name}: member/column {mName} not found. Filter: {filterTempl}");
          else
            log.LogError($"Index/key filter on entity {tbl.Entity.Name}: member/column {mName} not found. Filter: {filterTempl}");

        } //else
      }//foreach
      dbFilter.Columns.AddRange(colList);
      dbFilter.DefaultSql = GetKeyFilterSql(dbFilter);
      return dbFilter;
    }

    public string GetKeyFilterSql(DbTableFilter filter) {
      var cols = filter.Columns.Select(c => c.ColumnNameQuoted).ToList();
      var strCols = string.Join(", ", cols);
      var sql = string.Format(filter.EntityFilter.Template.StandardForm, strCols);
      return sql;
    }

    public virtual bool IsConversionRequired(UnaryExpression expression) {
      // obvious (and probably never happens), conversion to the same type
      if(expression.Type == expression.Operand.Type)
        return false;
      if(IsEnumAndInt(expression.Type, expression.Operand.Type))
        return false;
      //RI: trying to prevent CONVERT in order by clause
      if(expression.Type == typeof(object))
        return false;
      //RI: special case - do not convert bit column expression to int - they are already ints
      if(expression.Type == typeof(int) && expression.Operand is ColumnExpression && expression.Operand.Type == typeof(bool))
        return false;
      // second, nullable to non-nullable for the same type
      if(expression.Type.IsNullableValueType() && !expression.Operand.Type.IsNullableValueType()) {
        if(expression.Type.GetUnderlyingStorageClrType() == expression.Operand.Type)
          return false;
      }
      // third, non-nullable to nullable
      if(!expression.Type.IsNullableValueType() && expression.Operand.Type.IsNullableValueType()) {
        if(expression.Type == expression.Operand.Type.GetUnderlyingStorageClrType())
          return false;
      }
      // found no excuse not to convert? then convert
      return true;
    }

    private static bool IsEnumAndInt(Type x, Type y) {
      return x.IsEnum && Enum.GetUnderlyingType(x) == y ||
             y.IsEnum && Enum.GetUnderlyingType(y) == x;
    }

    // Used by DbInfo module
    public virtual string GetTableExistsSql(DbTableInfo table) {
      var template = @"
SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='{0}' AND TABLE_SCHEMA = '{1}'";
      var sql = string.Format(template, table.TableName, table.Schema);
      return sql;
    }
    public virtual string GetTableSelectAllSql(DbTableInfo table) {
      var template = @"SELECT * FROM {0};";
      var sql = string.Format(template, table.FullName);
      return sql;
    }

    public virtual IDbDataParameter AddDbParameter(IDbCommand command, SqlPlaceHolder ph, object value) {
      var prmIndex = command.Parameters.Count;
      var prm = command.CreateParameter();
      prm.ParameterName = GetSqlParameterName(prmIndex);
      prm.Direction = ph.ParamDirection;
      prm.Value = value ?? DBNull.Value;
      ph.PreviewParameter?.Invoke(prm, ph);
      command.Parameters.Add(prm);
      return prm;
    }

    public virtual string ListToLiteral(object value, DbTypeDef elemTypeDef) {
      var list = value as IList;
      if (list.Count == 0)
        return GetEmptyListLiteral(elemTypeDef);
      var strList = new List<string>(list.Count);
      foreach (var item in list) {
        strList.Add(elemTypeDef.ToLiteral(item));
      }
      return string.Join(", ", strList);
    }

    // Used in 't.Col IN (<list>)' expressions when list is empty; does not work for all providers
    public virtual string GetEmptyListLiteral(DbTypeDef elemTypeDef) {
      return "SELECT NULL WHERE 1=0"; //works OK for MS SQL, SQLite
    }

    // metaObject depends on statement type - LinqCommand, or DbTable, etc
    public virtual void ReviewSqlStatement(SqlStatement sql, object metaObject) {


    }



    #region Cached SQL param names
    // We pre-create parameter names to avoid string allocations at runtime
    string[] _sqlParamNames;
    protected void InitSqlParamNames() {
      // Create array of param names
      _sqlParamNames = new string[this.MaxParamCount + 100]; // +100 just in case; and to make unit test easier
      for(int i = 0; i < _sqlParamNames.Length; i++)
        _sqlParamNames[i] = this.DynamicSqlParameterPrefix + i;
    }

    public string GetSqlParameterName(int index) {
      if(_sqlParamNames == null)
        InitSqlParamNames(); //we don't need lock here
      return _sqlParamNames[index];
    }
    #endregion 




  } //class
}
