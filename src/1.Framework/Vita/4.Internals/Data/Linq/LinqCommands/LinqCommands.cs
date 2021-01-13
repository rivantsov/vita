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

  /*
  public class LinqCommand: LinqCommandBase {
    public SpecialSelectType SelectType;
    public EntityKeyInfo Key;
    public IList<EntityKeyMemberInfo> OrderBy;

    public LinqCommand(SpecialSelectType selectType, EntityKeyInfo key, LockType lockType, IList<EntityKeyMemberInfo> orderBy)
                                 : base(LinqCommandKind.SpecialSelect, LinqOperation.Select) {
      SelectType = selectType;
      Key = key;
      LockType = lockType; 
      OrderBy = orderBy;
      base.EntityTypes.Add(key.Entity.EntityType);
      base.SqlCacheKey = SqlCacheKeyBuilder.BuildSpecialSelectKey(selectType, key.Entity.Name, key.Name, lockType, orderBy);
    }
  }
  */

  /*
public class LinqCommand : LinqCommandBase {
  // source properties, set at creation

  //analysis results


  public LinqCommand(LinqCommandKind kind, LinqOperation op, LambdaExpression lambda = null, EntityInfo updateEntity = null)
                     : base(kind, op) {
    Kind = kind; 
    Lambda = lambda;
    UpdateEntity = updateEntity; 
  }
}

public class LinqCommand : LinqCommand {
  public Expression Expression;
  public EntitySession Session;
  public List<Expression> Locals = new List<Expression>();
  public object[] LocalValues;

  public LinqCommand(EntitySession session, LinqCommandKind kind, LinqOperation op, Expression expr, EntityInfo updateEntity = null)
                 :base(kind, op, null, updateEntity)  {
    Session = session; 
    Expression = expr;
    LinqCommandAnalyzer.Analyze(this); 
  }
  public override string ToString() {
    return Expression + string.Empty;
  }
}

public class LinqCommand {
  public LinqCommandBase BaseCommand;
  public object[] ParamValues;

  public LinqCommand(LinqCommandBase baseCommand) {
    BaseCommand = baseCommand; 
  }

  public LinqCommand(LinqCommandBase command, object[] paramValues) {
    BaseCommand = command;
    ParamValues = paramValues; 
  }

  // For pre-built queries, param values are set explicitly. For dynamic linq queries (and views)
  // they copied from locals
  public void CopyParamValuesFromLocal() {
    var dynCmd = BaseCommand as LinqCommand;
    if (dynCmd != null) {
      ParamValues = dynCmd.LocalValues;
    }
  }//method

} //class
*/

}
