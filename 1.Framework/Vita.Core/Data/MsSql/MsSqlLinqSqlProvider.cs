#region MIT license
// 
// MIT license
//
// Copyright (c) 2007-2008 Jiri Moudry, Pascal Craponne
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;

using Vita.Common; 
using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Linq.Translation.SqlGen;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Linq;
using System.Collections;

namespace Vita.Data.MsSql {

  public  class MsSqlLinqSqlProvider : Vita.Data.Driver.LinqSqlProvider {
    public readonly MsSqlVersion ServerVersion;

    public MsSqlLinqSqlProvider(DbModel dbModel, MsSqlVersion serverVersion) : base(dbModel) {
      ServerVersion = serverVersion;
    }

    public override SelectExpression PreviewSelect(SelectExpression e) {
      base.PreviewSelect(e);
      // SQL Server doesn't support 'ORDER BY' for 'SELECT COUNT(*)'
      if (e.HasOrderBy() && e.HasOutAggregates())
        e.OrderBy.Clear();
        
      return e;
    }

    public override string GetParameter(ExternalValueExpression parameter) {
      var baseValue = base.GetParameter(parameter);
      //Handling list-type parameters
      if(!parameter.IsList)
        return baseValue;
      // We use Sql_variant column in table UDT that holds list. It was found that it causes index scan instead of seek
      // So we add CAST here
      var elType = parameter.ListElementType;
      var template = @"(SELECT ""Value"" FROM {0})";
      if (elType == typeof(string))
        template = @"(SELECT CAST(""Value"" AS NVarchar) FROM {0})";
      else if (elType == typeof(Guid))
        template = @"(SELECT CAST(""Value"" AS uniqueidentifier) FROM {0})";
      else if (elType == typeof(long) || elType == typeof(ulong))
        template = @"(SELECT CAST(""Value"" AS bigint) FROM {0})";
      else if (elType.IsInt() || elType.IsEnum)
        template = @"(SELECT CAST(""Value"" AS int) FROM {0})";
      return string.Format(template, baseValue);
    }

    // MS SQL has restrictions on DB Views, so that you have to use COUNT_BIG instead of COUNT,
    // so we always use it and it seems to work
    protected override SqlStatement GetLiteralCount(SqlStatement a) {
      return SqlStatement.Format("COUNT_BIG({0})", a);
    }

    public override string GetTable(TableExpression tableExpression) {
      var options = tableExpression.LockOptions;
      string hint = null;
      if (options.IsSet(LockOptions.ForUpdate))
        hint = "UpdLock";
        // hint = "RepeatableRead, RowLock";
      else if (options.IsSet(LockOptions.SharedRead))
        hint = null; // "Serializable"; //turns out we do not need any hint here if we run in Snapshot level
        // hint = "RepeatableRead, RowLock";
      else if (options.IsSet(LockOptions.NoLock))
        hint = "NOLOCK";
      var table = base.GetTable(tableExpression);
      //Note: for MS SQL, table alias (if present) goes before the hint ( ... FROM Tbl t WITH(NOLOCK) ...), so it works as coded
      if (hint!=null)
        table += " WITH(" + hint + ")";
      return table;
    }

    public override string GetSubQueryAsAlias(string subquery, string alias)
    {
        return string.Format("({0}) AS {1}", subquery, GetTableAlias(alias));
    }

    //RI: added this to handle Guids
    public override SqlStatement GetLiteral(object literal) {
      if (literal is Guid)
        return "'" + literal.ToString() + "'";
      return base.GetLiteral(literal);
    }

    public override SqlStatement GetLiteral(bool literal)
    {
        if (literal)
            return "1";
        return "0";
    }

    // MS SQL does not allow '=' of bool values (bit is OK)
    protected override SqlStatement GetConvertBoolToBit(SqlStatement arg0) {
      return string.Format("IIF({0}, 1, 0)", arg0);
    }

    public override SqlStatement GetLiteralLimit(SqlStatement select, SqlStatement limit, SqlStatement offset, SqlStatement offsetAndLimit) {
      if(ServerVersion == MsSqlVersion.V2008)
        return GetLiteralLimit2008(select, limit, offset, offsetAndLimit);
      // 2012 and up
      var strOffset = offset == null ? "0" : offset.ToString();
      var strLimit = limit == null ? "1000000" : limit.ToString(); 
      return SqlStatement.Format("{0} \r\n OFFSET {1} ROWS FETCH NEXT {2} ROWS ONLY", select, strOffset, strLimit);
    }


    public SqlStatement GetLiteralLimit2008(SqlStatement select, SqlStatement limit, SqlStatement offset, SqlStatement offsetAndLimit) {

      //V2008
      //TODO: fix this, get rid of all this string matching
      var sql = select.ToString();
      var from = "\r\nFROM ";
      var orderBy = "\r\nORDER BY ";
      var selectK = "SELECT ";
      int fromIdx = sql.IndexOf(from);
      int orderByIdx = sql.IndexOf(orderBy);

      if (fromIdx < 0)
        throw new ArgumentException("S0051: Unknown select format: " + sql);

      string orderByClause = null;
      string sourceClause = null;
      if (orderByIdx >= 0) {
        orderByClause = sql.Substring(orderByIdx);
        sourceClause = sql.Substring(fromIdx, orderByIdx - fromIdx);
      } else {
        orderByClause = "ORDER BY " + sql.Substring(selectK.Length, fromIdx - selectK.Length);
        sourceClause = sql.Substring(fromIdx);
      }
      orderByClause = orderByClause.Replace("\r\n", string.Empty); 

      var selectFieldsClause = sql.Substring(0, fromIdx);

      var finalSql = SqlStatement.Format(
            "SELECT *{0}" +
            "FROM ({0}" +
            "    {1},{0}" +
            "    ROW_NUMBER() OVER({2}) AS [__ROW_NUMBER]{0}" +
            "    {3}" +
            "    ) AS [t0]{0}" +
            "WHERE [__ROW_NUMBER] BETWEEN {4}+1 AND {4}+{5}{0}" +
            "ORDER BY [__ROW_NUMBER]",
            NewLine, selectFieldsClause, orderByClause, sourceClause, offset, limit);
      return finalSql;
    }

    protected override SqlStatement GetLiteralDateDiff(SqlStatement dateA, SqlStatement dateB)
    {
        return SqlStatement.Format("(CONVERT(BigInt,DATEDIFF(DAY, {0}, {1}))) * 86400000 +" //diffierence in milliseconds regards days
                  + "DATEDIFF(MILLISECOND, "

                            // (DateA-DateB) in days +DateB = difference in time
                            + @"DATEADD(DAY, 
                                  DATEDIFF(DAY, {0}, {1})
                                  ,{0})"

                            + ",{1})", dateB, dateA);

        //this trick is needed in sqlserver since DATEDIFF(MILLISECONDS,{0},{1}) usually crhases in the database engine due an overflow:
        //System.Data.SqlClient.SqlException : Difference of two datetime columns caused overflow at runtime.
    }

    protected override SqlStatement GetLiteralDateTimePart(SqlStatement dateExpression, SqlFunctionType operationType) {
      switch(operationType) {
        case SqlFunctionType.Week:
          return SqlStatement.Format("DATEPART(ISOWK,{0})", dateExpression);
        case SqlFunctionType.Date:
          return SqlStatement.Format("CONVERT(DATE, {0})", dateExpression);
        case SqlFunctionType.Time:
          return SqlStatement.Format("CONVERT(TIME, {0})", dateExpression);
        default:
          return SqlStatement.Format("DATEPART({0},{1})", operationType.ToString().ToUpper(), dateExpression);
      }
    }


    protected override SqlStatement GetLiteralMathPow(SqlStatement p, SqlStatement p_2)
    {
        return SqlStatement.Format("POWER({0},{1})", p, p_2);
    }

    protected override SqlStatement GetLiteralMathLog(SqlStatement p, SqlStatement p_2)
    {
        return SqlStatement.Format("(LOG({0})/LOG({1}))", p, p_2);
    }

    protected override SqlStatement GetLiteralMathLn(SqlStatement p)
    {
        return GetLiteralMathLog(p, string.Format("{0}", Math.E));
    }

    protected override SqlStatement GetLiteralStringLength(SqlStatement a)
    {
        return SqlStatement.Format("LEN({0})", a);
    }

    protected override SqlStatement GetLiteralSubString(SqlStatement baseString, SqlStatement startIndex, SqlStatement count)
    {
        //in standard sql base string index is 1 instead 0
        return SqlStatement.Format("SUBSTRING({0}, {1}, {2})", baseString, startIndex, count);
    }

    protected override SqlStatement GetLiteralSubString(SqlStatement baseString, SqlStatement startIndex)
    {
        return GetLiteralSubString(baseString, startIndex, GetLiteralStringLength(baseString));
    }

    protected override SqlStatement GetLiteralTrim(SqlStatement a)
    {
        return SqlStatement.Format("RTRIM(LTRIM({0}))", a);
    }

    protected override SqlStatement GetLiteralStringConcat(SqlStatement a, SqlStatement b)
    {
        return SqlStatement.Format("{0} + {1}", a, b);
    }

    protected override SqlStatement GetLiteralStringToLower(SqlStatement a)
    {
        return SqlStatement.Format("LOWER({0})", a);
    }

    protected override SqlStatement GetLiteralStringToUpper(SqlStatement a)
    {
        return SqlStatement.Format("UPPER({0})", a);
    }

    protected override SqlStatement GetLiteralStringIndexOf(SqlStatement baseString, SqlStatement searchString)
    {
        return GetLiteralSubtract(SqlStatement.Format("CHARINDEX({0},{1})", searchString, baseString), "1");
    }

    protected override SqlStatement GetLiteralStringIndexOf(SqlStatement baseString, SqlStatement searchString, SqlStatement startIndex)
    {
        return GetLiteralSubtract(SqlStatement.Format("CHARINDEX({0},{1},{2})", searchString, baseString, startIndex), "1");
    }

    protected override SqlStatement GetLiteralStringIndexOf(SqlStatement baseString, SqlStatement searchString, SqlStatement startIndex, SqlStatement count)
    {
        return GetLiteralSubtract(SqlStatement.Format("CHARINDEX({0},{1},{2})", searchString, GetLiteralSubString(baseString, "1", GetLiteralStringConcat(count, startIndex)), startIndex), "1");
    }

    //http://msdn.microsoft.com/en-us/library/4e5xt97a(VS.71).aspx
    public static readonly Dictionary<Type, string> typeMapping = new Dictionary<Type, string>
    {
        {typeof(int),"int"},
        {typeof(uint),"int"},

        {typeof(long),"bigint"},
        {typeof(ulong),"bigint"},

        {typeof(float),"float"}, //TODO: could be float or real. check ranges.
        {typeof(double),"float"}, //TODO: could be float or real. check ranges.
            
        {typeof(decimal),"numeric"},

        {typeof(short),"tinyint"},
        {typeof(ushort),"tinyint"},

        {typeof(bool),"bit"},

        // trunk? They could be: varchar, char,nchar, ntext,text... it should be the most flexible string type. TODO: check wich of them is better.
        {typeof(string),"nvarchar(max)"}, 
        {typeof(char[]),"varchar"},

        {typeof(char),"char"},

        {typeof(DateTime),"datetime"},
        {typeof(Guid),"uniqueidentifier"}

        // there are more types: timestamps, images ... TODO: check what is the official behaviour
    };

    public override SqlStatement GetLiteralConvert(SqlStatement a, Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            type = type.GetGenericArguments().First();

        if (type.IsValueType && a[0].Sql.StartsWith("@"))
            return a;

        SqlStatement sqlTypeName;
        if (typeMapping.ContainsKey(type))
            sqlTypeName = typeMapping[type];
        else
            sqlTypeName = "sql_variant";

        return SqlStatement.Format("CONVERT({0},{1})", sqlTypeName, a);
    }

    public override string GetColumn(string table, string column)
    {
        if (column != "*")
            return base.GetColumn(table, column);
        return "*";
    }

    protected override SqlStatement GetNewGuid() {
      return "NewId()";
    }

    public override bool IsSqlTier(System.Linq.Expressions.Expression expression, LinqCommandKind queryUse) {
      //MS SQL does not allow comparison operations in SELECT list
      if (expression.Type == typeof(bool)) {
        switch (expression.NodeType) {
          case ExpressionType.LessThan: case ExpressionType.LessThanOrEqual:
          case ExpressionType.GreaterThan:  case ExpressionType.GreaterThanOrEqual:
          case ExpressionType.Equal:  case ExpressionType.NotEqual:
            return false; //means SQL wouldn't support it
        }
      }//if
      return base.IsSqlTier(expression, queryUse);
    }

    public override void SetDbParameterValue(IDbDataParameter parameter, Type type, object value) {
      if (value == null)
        return;
      //Check for array
      Type elemType;
      // quick check for array or gen type, then deeper check for list of db primitives
      if ((type.IsArray || type.IsGenericType) && type.IsListOfDbPrimitive(out elemType)) {
        var sqlParam = (SqlParameter)parameter;
        sqlParam.SqlDbType = SqlDbType.Structured;
        sqlParam.TypeName = GetArrayAsTableTypeName();
        sqlParam.Value = MsSqlDbDriver.ConvertListToRecordList(value as IEnumerable);
        return;
      }
      base.SetDbParameterValue(parameter, type, value);
      if (parameter.DbType == DbType.DateTime)
        parameter.DbType = DbType.DateTime2;
    }

    private string _arrayAsTableTypeName;
    //Note: we cannot do this in constructor, Linq provider is created early in DbModel construction, when 
    // other stuff is not created yet.
    private string GetArrayAsTableTypeName() {
      if (_arrayAsTableTypeName == null) {
        var tpInfo = this.DbModel.CustomDbTypes.FirstOrDefault(t => t.Kind == DbCustomTypeKind.ArrayAsTable);
        Util.Check(tpInfo != null, "Cannot find User-defined type for passing arrays in parameters.");
        _arrayAsTableTypeName = tpInfo.FullName;
      }
      return _arrayAsTableTypeName;
    }


  }//class
}
