using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Data.Linq.Translation.Expressions {

  public class AliasedExpression : SqlExpression {
    public readonly Expression Expression;

    public AliasedExpression(Expression expression, string alias) : base(SqlExpressionType.Alias, expression.Type) {
      Expression = expression;
      Alias = alias; 
    }
  }
}
