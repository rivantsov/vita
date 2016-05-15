
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;

using Vita.Common;

namespace Vita.Data.Linq.Translation.Expressions
{
    /// <summary>
    /// A GroupExpression holds a grouped result
    /// It is usually transparent, except for return value, where it mutates the type to IGrouping
    /// </summary>
    [DebuggerDisplay("GroupExpression: {GroupedExpression}")]
    public class GroupExpression : SqlExpression  {

        public Expression GroupedExpression { get; private set; }
        public Expression KeyExpression { get; private set; }

        public IList<ColumnExpression> Columns { get; private set; }

        public bool UseClrGrouping; //if true, grouping will be performed in CLR, not in SQL

        public GroupExpression(Expression groupedExpression, Expression keyExpression, IList<ColumnExpression> columns, bool useClrGrouping = false)
            : base(SqlExpressionType.Group, groupedExpression.Type){
            GroupedExpression = groupedExpression;
            KeyExpression = keyExpression;
            Columns = columns;
            UseClrGrouping = useClrGrouping;
        }

        public bool IsDistinct {
          get { return GroupedExpression == KeyExpression; }
        }

        public override Expression Mutate(IList<Expression> newOperands)
        {
            if (newOperands.Count > 0)
                Util.Throw("S0065: Don't Mutate() a GroupExpression");
            return this;
        }
    }
}