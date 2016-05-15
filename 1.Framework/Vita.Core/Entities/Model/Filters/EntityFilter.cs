using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Runtime;

namespace Vita.Entities.Model {

  // Filter for evaluating loaded entities in .net runtime (not in SQL)
  public class EntityPredicate : FilterPredicate {
    public Delegate CompiledLambda;
    public EntityPredicate(LambdaExpression lambda) : base(lambda) {
      var rewriter = new SafeMemberAccessRewriter();
      var safeLambda = rewriter.Rewrite(lambda);
      CompiledLambda = safeLambda.Compile();
    }
    
    public bool Evaluate(EntitySession session, object entity) {
      var args = new object[Lambda.Parameters.Count];
      args[0] = entity;
      for(int i = 1; i < Lambda.Parameters.Count; i++)
        args[i] = session.EvaluateLambdaParameter(Lambda.Parameters[i]);
      var result = CompiledLambda.DynamicInvoke(args);
      return (bool)result;
    }
  }

  public class EntityFilter : Dictionary<Type, EntityPredicate> {
    public EntityPredicate GetPredicate(Type entityType) {
      EntityPredicate result;
      if(base.TryGetValue(entityType, out result))
        return result;
      return null;
    }
  }

  
}
