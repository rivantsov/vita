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
using System.Linq;
using System.Collections.Generic;
using System.Data;

using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Linq.Translation.SqlGen;
using Vita.Entities;
using Vita.Data.Model;
using Vita.Data.Driver;
using System.Linq.Expressions;
using Vita.Common;

namespace Vita.Data.SqlCe {
    
    public class SqlCeLinqSqlProvider : Vita.Data.Driver.LinqSqlProvider   {

      public SqlCeLinqSqlProvider(DbModel dbModel) : base(dbModel) { }

      public override SelectExpression PreviewSelect(SelectExpression e) {
        base.PreviewSelect(e);
        // SQL CE doesn't support 'ORDER BY' for 'SELECT COUNT(*)'
        if (e.HasOrderBy() && e.HasOutAggregates())
          e.OrderBy.Clear();
        return e;
      }

      private DateTime MinDateTime = new DateTime(1753, 1, 1); //minimum value for SQL DateTime type
      private DateTime MaxDateTime = new DateTime(9999, 12, 31); //minimum value for SQL DateTime type

      public override void SetDbParameterValue(IDbDataParameter parameter, Type type, object value) {
        if (type == typeof(DateTime)) {
          var dtValue = (DateTime)value;
          if (dtValue < MinDateTime)
            value = MinDateTime;
          if (dtValue > MaxDateTime)
            value = MaxDateTime;
        } 
        base.SetDbParameterValue(parameter, type, value);
      }

      /// <summary>
      /// Returns a table alias
      /// Ensures about the right case
      /// </summary>
      /// <param name="subquery"></param>
      /// <param name="alias"></param>
      /// <returns></returns>
      public override string GetSubQueryAsAlias(string subquery, string alias)
      {
          return string.Format("({0}) AS {1}", subquery, GetTableAlias(alias));
      }

      // does not allow '=' of bool values (bit is OK)
      protected override SqlStatement GetConvertBoolToBit(SqlStatement arg0) {
        return string.Format("IIF({0}, 1, 0)", arg0);
      }

      //RI: added this to handle Guids
      public override SqlStatement GetLiteral(object literal) {
        if (literal is Guid)
          return "'" + literal.ToString() + "'";
        if(literal is DateTime) {
          //Datetime should not have second fractions
          var dt = (DateTime)literal;
          var str = "'" + dt.ToString("s") + "'";
          return str; 
        }
        return base.GetLiteral(literal);
      }

      public override SqlStatement GetLiteral(bool literal)
      {
          if (literal)
              return "1";
          return "0";
      }

      public override string GetParameterName(string nameBase)
      {
          return string.Format("@{0}", nameBase);
      }

      public override SqlStatement GetLiteralLimit(SqlStatement select, SqlStatement limit, SqlStatement offset, SqlStatement offsetAndLimit)
      {
        //SQL CE specific format
        return SqlStatement.Format("{0} \r\n OFFSET {1} ROWS FETCH NEXT {2} ROWS ONLY", select, offset, limit);
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

      protected override SqlStatement GetLiteralDateTimePart(SqlStatement dateExpression, SqlFunctionType operationType)
      {
        switch (operationType) {
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

      protected override SqlStatement GetLiteralStringToLower(SqlStatement a)
      {
          return SqlStatement.Format("LOWER({0})", a);
      }
      protected override SqlStatement GetLiteralStringConcat(SqlStatement a, SqlStatement b) {
        return SqlStatement.Format("{0} + {1}", a, b);
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
      static readonly Dictionary<Type, string> _typeMappingsForConvert = new Dictionary<Type, string>
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
          {typeof(string),"nvarchar(100)"}, 
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
          if (_typeMappingsForConvert.ContainsKey(type))
              sqlTypeName = _typeMappingsForConvert[type];
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

      protected override SqlStatement GetEqualNullables(SqlStatement a, SqlStatement b) {
        // Default impl:
        //    return SqlStatement.Format("({0} = {1} OR ({0} IS NULL) AND ({1} IS NULL))", a, b);
        // SQL CE has some strange restrictions on use of Parameters/IsNull methods and Memo fields
        //  for ex: (@P1 IS NULL) is not supported. So we fall back to reqular Equal statement
        return SqlStatement.Format("{0} = {1}", a, b);
      }

      protected override SqlStatement GetNewGuid() {
        return "NewId()";
      }
      public override bool IsSqlTier(System.Linq.Expressions.Expression expression, Vita.Entities.Linq.LinqCommandKind queryUse) {
        //MS SQL does not allow comparison operations in SELECT list
        if (expression.Type == typeof(bool)) {
          switch (expression.NodeType) {
            case ExpressionType.LessThan: case ExpressionType.LessThanOrEqual:
            case ExpressionType.GreaterThan: case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.Equal: case ExpressionType.NotEqual:
              return false; //means SQL wouldn't support it
          }
        }//if
        return base.IsSqlTier(expression, queryUse);
      }
      

    }
}
