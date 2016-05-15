using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Locking;

namespace Vita.Entities.Linq {


  public class EntityQuery : IQueryable, IOrderedQueryable {

    public EntityQuery(IQueryProvider provider, Type elementType, Expression expression = null) {
      Provider = provider ?? new EntityQueryProvider();
      ElementType = elementType;
      Expression = expression;
    }

    public override string ToString() {
      return Expression.ToString();
    }

    public bool IsEntitySet { get; protected set; }

    #region IQueryable Members
    public virtual Type ElementType { get; private set; }
    public Expression Expression { get; protected set; }
    public IQueryProvider Provider { get; protected set;}
    #endregion


    #region IEnumerable Members
    public IEnumerator GetEnumerator() {
      var iEnum = (IEnumerable)Provider.Execute(this.Expression);
      return iEnum.GetEnumerator();
    }
    #endregion
  }

  public class EntityQuery<TEntity> : EntityQuery, IQueryable<TEntity>, IOrderedQueryable<TEntity> {

    // Note: expression parameter is null for root entity set
    public EntityQuery(IQueryProvider provider, Expression expression) : base(provider, typeof(TEntity), expression) { 
    }

    #region IEnumerable<TEntity> Members
    IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator() {
      var results = this.Provider.Execute<IEnumerable<TEntity>>(this.Expression);
      return  results.GetEnumerator();
    }
    #endregion

  }//class

  public class EntitySet<TEntity> : EntityQuery<TEntity>, ILockTarget {
    public LockOptions LockOptions { get; private set; }
    
    public EntitySet(IQueryProvider provider = null, LockOptions lockOptions = LockOptions.None) : base(provider, null) {
      LockOptions = lockOptions; 
      Expression = Expression.Constant(this); 
      IsEntitySet = true;
    }
    public override string ToString() {
      var fmt = this.LockOptions == LockOptions.None ? "EntitySet<{0}>" : "EntitySet<{0}>({1})";
      return string.Format(fmt, ElementType.Name, this.LockOptions);
    }
  }

}
