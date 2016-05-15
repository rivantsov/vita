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
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Linq;
using Vita.Entities.Locking;
using Vita.Data.Linq.Translation.SqlGen;
using Vita.Data.Model;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Driver;

namespace Vita.Data.MySql {
    public class MySqlLinqSqlProvider : Vita.Data.Driver.LinqSqlProvider
    {
        public MySqlLinqSqlProvider(DbModel dbModel) : base(dbModel) { }

        public override SqlStatement ReviewSelectSql(SelectExpression select, SqlStatement sql) {
          const string NoLockTemplate =
@"SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
{0};
COMMIT ;  
";
          const string ReadLockTemplate = "{0} \r\n LOCK IN SHARE MODE;";
          const string WriteLockTemplate = "{0} \r\n FOR UPDATE;";
          var flags = select.CommandInfo.Flags;
          if (flags.IsSet(LinqCommandFlags.NoLock)) 
            return string.Format(NoLockTemplate, sql);
          if (flags.IsSet(LinqCommandFlags.ReadLock))
            return string.Format(ReadLockTemplate, sql);
          if (flags.IsSet(LinqCommandFlags.WriteLock))
            return string.Format(WriteLockTemplate, sql);
          return sql; 
        }

        public override Type GetSqlFunctionResultType(SqlFunctionType functionType, Type[] operandTypes) {
          switch(functionType) {
            case SqlFunctionType.Count: return typeof(long);
          }
          return base.GetSqlFunctionResultType(functionType, operandTypes);
        }

        public override string GetParameterName(string nameBase)
        {
            return string.Format("@{0}", nameBase);
        }

        protected override SqlStatement GetLiteralCount(SqlStatement a)
        {
            return "COUNT(*)";
        }

        public override SqlStatement GetLiteral(System.Guid guid) {
          return GetLiteralBinary(guid.ToByteArray());
        }

        public override SqlStatement GetLiteralBinary(byte[] value) {
          return "X'" + Vita.Common.HexUtil.ByteArrayToHex(value) + "'";
        }

        protected override SqlStatement GetLiteralStringConcat(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("CONCAT({0}, {1})", a, b);
        }
        
        public virtual string GetBulkInsert(string table, IList<string> columns, IList<IList<string>> valuesLists)
        {
            if (columns.Count == 0)
                return string.Empty;

            var insertBuilder = new StringBuilder("INSERT INTO ");
            insertBuilder.Append(table);
            insertBuilder.AppendFormat(" ({0})", string.Join(", ", columns.ToArray()));
            insertBuilder.Append(" VALUES ");
            var literalValuesLists = new List<string>();
            foreach (var values in valuesLists)
                literalValuesLists.Add(string.Format("({0})", string.Join(", ", values.ToArray())));
            insertBuilder.Append(string.Join(", ", literalValuesLists.ToArray()));
            return insertBuilder.ToString();
        }

        protected override SqlStatement GetNewGuid() {
          // UUID function returns string (new guid as string); we need to turn it into binary
          return "UNHEX(REPLACE(UUID(),'-',''))";
          //return "UUID()";
        }

        protected override SqlStatement GetLiteralDateTimePart(SqlStatement dateExpression, SqlFunctionType operationType) {
          switch (operationType) {
            case SqlFunctionType.Date:
              return SqlStatement.Format("DATE({0})", dateExpression);
            case SqlFunctionType.Week:
              return SqlStatement.Format("WEEK({0})", dateExpression);
            case SqlFunctionType.Year:
              return SqlStatement.Format("YEAR({0})", dateExpression);
            case SqlFunctionType.Month:
              return SqlStatement.Format("MONTH({0})", dateExpression);
            case SqlFunctionType.Day:
              return SqlStatement.Format("Day({0})", dateExpression);
            // case SqlFunctionType.Time: // TIME() returns string, so this does not work
              // return SqlStatement.Format("TIME({0})", dateExpression);

            default:
              Util.Throw("SQL function {0} not supported.", operationType);
              return null; 
          }
          
        }
    }
}
