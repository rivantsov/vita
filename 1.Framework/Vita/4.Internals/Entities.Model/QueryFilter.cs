using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Model {

  //Base abstract filter predicate - base for EntityPredicate and LinqPredicate
  public abstract class FilterPredicate {
    public Type EntityType;
    public LambdaExpression Lambda;
    private string _asString;

    public FilterPredicate(LambdaExpression lambda) {
      Util.CheckParam(lambda, nameof(lambda));
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

  // Predicate to include in LINQ queries as extra Where clauses

  public abstract class QueryPredicate : FilterPredicate {
    public QueryPredicate(LambdaExpression lambda) : base(lambda) { }
  }

  public class QueryPredicate<TEntity> : QueryPredicate {
    public Expression<Func<TEntity, bool>> Where;

    public QueryPredicate(LambdaExpression lambda)  : base(lambda) {
      Where = System.Linq.Expressions.Expression.Lambda<Func<TEntity, bool>>(lambda.Body, lambda.Parameters[0]);
    }
  }//class

  public class QueryFilter : Dictionary<Type, QueryPredicate> {
    public void Add<TEntity>(Expression<Func<TEntity, bool>> lambda) {
      AddEntry<TEntity>(lambda); 
    }
    public void Add<TEntity, T1>(Expression<Func<TEntity, T1, bool>> lambda) {
      AddEntry<TEntity>(lambda);
    }
    public void Add<TEntity, T1, T2>(Expression<Func<TEntity, T1, T2, bool>> lambda) {
      AddEntry<TEntity>(lambda);
    }
    public void Add<TEntity, T1, T2, T3>(Expression<Func<TEntity, T1, T2, T3, bool>> lambda) {
      AddEntry<TEntity>(lambda);
    }
    public void Add<TEntity, T1, T2, T3, T4>(Expression<Func<TEntity, T1, T2, T3, T4, bool>> lambda) {
      AddEntry<TEntity>(lambda);
    }

    private void AddEntry<TEntity>(LambdaExpression lambda) {
      var pred = new QueryPredicate<TEntity>(lambda);
      base.Add(typeof(TEntity), pred);
    }

    public QueryPredicate<TEntity> GetPredicate<TEntity>() {
      return (QueryPredicate<TEntity>)GetPredicate(typeof(TEntity));
    }
    public QueryPredicate GetPredicate(Type entityType) {
      QueryPredicate result;
      if(base.TryGetValue(entityType, out result))
        return result;
      return null;
    }
  }


}
