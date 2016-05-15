using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Data.Linq.Translation.Expressions {
  public class TableFilterExpression: SqlExpression {
    public TableExpression Table;
    public string Filter; 

    public TableFilterExpression(TableExpression table, string filter)  : base(SqlExpressionType.TableFilter, typeof(bool)) {
      Table = table;
      Filter = filter; 
    }

    public override Expression Mutate(System.Collections.Generic.IList<Expression> newOperands) {
      if (newOperands != null && newOperands.Count > 0)
        Table = (TableExpression) newOperands[0];
      return this;
    }

  }
}
