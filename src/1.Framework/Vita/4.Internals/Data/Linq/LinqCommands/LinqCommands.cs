using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq {
  using Vita.Data.Linq.Translation.Expressions;

  public enum LinqCommandKind {
    Special,
    Dynamic,
    View,
  }

  public enum LinqOperation {
    Select,
    Insert,
    Update,
    Delete,
  }

  public interface IMaskSource {
    EntityMemberMask GetMask(Type entityType);
  }

  public abstract class LinqCommand {
    public EntitySession Session;
    public LinqCommandKind Kind;
    public LinqOperation Operation;
    public string SqlCacheKey;
    public List<Type> EntityTypes = new List<Type>();
    public QueryOptions Options;
    public LockType LockType;
    public IMaskSource MaskingSource;

    public LambdaExpression Lambda;
    public object[] ParamValues;

    public List<LambdaExpression> Includes;
    public SelectExpression SelectExpression;


    public LinqCommand(EntitySession session, LinqCommandKind kind, LinqOperation operation) {
      Session = session; 
      Kind = kind;
      Operation = operation;
    }

  } //class

  public class DynamicLinqCommand : LinqCommand {
    public Expression QueryExpression;
    public EntityInfo UpdateEntity; //for LINQ non-query commands
    public ParameterExpression[] ExternalParameters;
    public List<Expression> Locals = new List<Expression>();

    public DynamicLinqCommand(EntitySession session, Expression queryExpression, LinqCommandKind kind = LinqCommandKind.Dynamic,
                        LinqOperation op = LinqOperation.Select, EntityInfo updateEntity = null)
                         : base(session, kind, op) {
      QueryExpression = queryExpression;
      UpdateEntity = updateEntity;
      LinqCommandAnalyzer.Analyze(this);
    }
  }

  public class SpecialLinqCommand : LinqCommand {
    public EntityKeyInfo Key;
    public List<EntityKeyMemberInfo> OrderBy;
    public Action<SpecialLinqCommand> SetupAction; //delayed creator of lambda expression
    public ChildEntityListInfo ListInfoManyToMany; 

    public SpecialLinqCommand(EntitySession session, string sqlCacheKey, 
                              EntityKeyInfo key, LockType lockType, List<EntityKeyMemberInfo> orderBy, 
                              object[] paramValues, Action<SpecialLinqCommand> setupAction)
                              : base(session, LinqCommandKind.Special, LinqOperation.Select) {
      this.SqlCacheKey = sqlCacheKey;
      Key = key;
      base.LockType = lockType;
      OrderBy = orderBy;
      ParamValues = paramValues;
      SetupAction = setupAction;
    }

    // constructor for many-to-many select
    public SpecialLinqCommand(EntitySession session, string sqlCacheKey, 
                              ChildEntityListInfo listInfoManyToMany, List<EntityKeyMemberInfo> orderBy,
                              object[] paramValues, Action<SpecialLinqCommand> setupAction)
                            : base(session, LinqCommandKind.Special, LinqOperation.Select) {
      this.SqlCacheKey = sqlCacheKey;
      ListInfoManyToMany = listInfoManyToMany;
      OrderBy = orderBy;
      ParamValues = paramValues;
      SetupAction = setupAction;
      base.LockType = LockType.None;
    }
  }

}
