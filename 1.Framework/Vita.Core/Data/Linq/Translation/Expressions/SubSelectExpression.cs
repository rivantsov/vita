
using System;
using System.Diagnostics;
using System.Linq.Expressions;

using Vita.Data.Linq.Translation.Expressions;

namespace Vita.Data.Linq.Translation.Expressions
{
    /// <summary>
    /// A table expression produced by a sub select, which work almost like any other table
    /// Different joins specify different tables
    /// </summary>
    [DebuggerDisplay("SubSelectExpression {Name} (as {Alias})")]
    public class SubSelectExpression : TableExpression
    {
        public SelectExpression Select { get; private set; }

        public SubSelectExpression(SelectExpression select, Type type, string alias, 
                     Vita.Data.Model.DbTableInfo tableInfo)  : base(tableInfo, type, alias)
        {
            this.Select = select;
            this.Alias = alias;
        }

        public override bool IsEqualTo(TableExpression expression)
        {
            SubSelectExpression subSelectTable = expression as SubSelectExpression;
            if (subSelectTable == null)
                return false;
            return Name == expression.Name && JoinID == expression.JoinID && Select == subSelectTable.Select;
        }
    }
}
