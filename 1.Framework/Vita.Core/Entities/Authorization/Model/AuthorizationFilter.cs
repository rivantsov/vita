using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Authorization {

  [Flags]
  public enum FilterUse {
    None = 0,
    Query = 1, // OK to use in Linq/SQL queries
    Entities = 1 << 1, //Use on entities 

    All = Query | Entities,
  }

  public class AuthorizationFilter {
    public string Name; 
    public readonly EntityFilter EntityFilter = new EntityFilter();
    public readonly QueryFilter QueryFilter = new QueryFilter();

    public AuthorizationFilter(string name) {
      Name = name; 
    }

    public void Add<TEntity>(Expression<Func<TEntity, bool>> lambda, FilterUse filterUse = FilterUse.Entities) {
      AddEntry<TEntity>(lambda, filterUse); 
    }
    
    public void Add<TEntity, TP1>(Expression<Func<TEntity, TP1, bool>> lambda, FilterUse filterUse = FilterUse.Entities) {
      AddEntry<TEntity>(lambda, filterUse);
    }
    
    public void Add<TEntity, TP1, TP2>(Expression<Func<TEntity, TP1, TP2, bool>> lambda, 
                                       FilterUse filterUse = FilterUse.Entities) {
      AddEntry<TEntity>(lambda, filterUse);
    }
    
    public void Add<TEntity, TP1, TP2, TP3>(Expression<Func<TEntity, TP1, TP2, TP3, bool>> lambda, 
                                            FilterUse filterUse = FilterUse.Entities) {
      AddEntry<TEntity>(lambda, filterUse);
    }

    public void Add<TEntity, TP1, TP2, TP3, TP4>(Expression<Func<TEntity, TP1, TP2, TP3, TP4, bool>> lambda,
                                            FilterUse filterUse = FilterUse.Entities) {
      AddEntry<TEntity>(lambda, filterUse);
    }


    private void AddEntry<TEntity>(LambdaExpression lambda, FilterUse filterUse) {
      if (filterUse.IsSet(FilterUse.Entities))
        EntityFilter.Add(typeof(TEntity), new EntityPredicate(lambda));
      if(filterUse.IsSet(FilterUse.Query))
        QueryFilter.Add(typeof(TEntity), new QueryPredicate<TEntity>(lambda));
    }

    public bool MatchEntity(OperationContext context, object entity) {
      Util.Check(entity != null, "Entity may not be null.");
      var type = EntityHelper.GetEntityType(entity);
      var entSetFilter = EntityFilter.GetPredicate(type);
      if(entSetFilter == null)
        return false;
      var session =  EntityHelper.GetSession(entity);
      var result = entSetFilter.Evaluate((EntitySession)session, entity);
      return result;
    }

    public bool HasPredicate(Type entityType) {
      return EntityFilter.ContainsKey(entityType) || QueryFilter.ContainsKey(entityType); 
    }
  }//class
}
