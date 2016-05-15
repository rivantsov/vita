
using System; 
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Data.Model;

namespace Vita.Data.Linq.Translation.Expressions
{
    /// <summary>
    /// Describes a column, related to a table
    /// </summary>
    [DebuggerDisplay("ColumnExpression {Table.Name} (as {Table.Alias}).{Name}")]
    public class ColumnExpression : SqlExpression {
        public TableExpression Table { get; private set; }
        public string Name { get; private set; }
        public DbColumnInfo ColumnInfo; 

        public ColumnExpression(TableExpression table, DbColumnInfo columnInfo) : base(SqlExpressionType.Column, GetMemberType(columnInfo)) {
            Table = table;
            ColumnInfo = columnInfo; 
            Name = ColumnInfo.ColumnName;
        }
        private static Type GetMemberType(DbColumnInfo columnInfo) {
          return columnInfo.Member.DataType; 
        }
 
    }
}