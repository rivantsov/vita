using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using Vita.Entities;
using Vita.Entities.Locking;

namespace Vita.Data.Linq {

  public class LinqCommandInfo {
    public string CacheKey;
    public List<Expression> Locals;
    public List<LambdaExpression> Includes;
    public LockType LockType;
    public QueryOptions Options; 

    public LinqCommandInfo(string cacheKey, QueryOptions options, List<Expression> locals,
                             List<LambdaExpression> includes) {
      CacheKey = cacheKey;
      Options = options;
      Locals = locals;
      Includes = includes; 
    }
  }
}
