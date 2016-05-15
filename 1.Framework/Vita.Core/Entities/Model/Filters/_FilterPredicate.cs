using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;

namespace Vita.Entities.Model {
  //Base abstract filter predicate - base for EntityPredicate and LinqPredicate
  public abstract class FilterPredicate {
    public Type EntityType;
    public LambdaExpression Lambda;
    private string _asString;

    public FilterPredicate(LambdaExpression lambda) {
      Util.Check(lambda.Parameters.Count > 0, "Invalid FilterPredicate lambda - must have at least one Entity parameter.");
      Util.Check(lambda.Body.Type == typeof(bool), "Invalid FilterPredicate lambda - must have bool return type.");
      Lambda = lambda;
      EntityType = Lambda.Parameters[0].Type;
      _asString = lambda.ToString();
    }
    public override string ToString() {
      return _asString;
    }

  } // EntitySetFilter class

}
