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

  public enum SpecialCommandSubType {
    SelectByKey,
    SelectByKeyArray,
    ExistsByKey
  }

  public interface IMaskSource {
    EntityMemberMask GetMask(Type entityType);
  }

  public class LinqCommand {
    public LinqCommandKind Kind;
    public LinqOperation Operation;
    public string SqlCacheKey;
    public List<Type> EntityTypes = new List<Type>();
    public QueryOptions Options;
    public LockType LockType;
    public IMaskSource MaskingSource;

    public EntityInfo UpdateEntity; //for LINQ non-query commands

    public LambdaExpression Lambda;
    public Action<LinqCommand> SetupAction; //delayed creator of lambda expression
    public QueryResultShape ResultShape;

    public EntitySession Session;
    public List<Expression> Locals = new List<Expression>();
    public object[] LocalValues;
    public Expression QueryExpression;

    public List<LambdaExpression> Includes;
    public ParameterExpression[] ExternalParameters;
    public object[] ParamValues;


    public LinqCommand(EntitySession session, LinqCommandKind kind, LinqOperation operation) {
      Session = session; 
      Kind = kind;
      Operation = operation;
    }

    public LinqCommand(EntitySession session, Expression queryExpression, LinqCommandKind kind = LinqCommandKind.Dynamic, 
                        LinqOperation op = LinqOperation.Select, EntityInfo updateEntity = null, Action<LinqCommand> setup = null)
                         : this(session, kind, op){
      QueryExpression = queryExpression;
      UpdateEntity = updateEntity;
      SetupAction = setup; 
      LinqCommandAnalyzer.Analyze(this);
    }

    public void CopyParamValuesFromLocal() {

    }
    
  } //class

  public class SpecialLinqCommand : LinqCommand {
    public SpecialCommandSubType SubType;
    public EntityKeyInfo Key;
    public List<EntityKeyMemberInfo> OrderBy;

    public SpecialLinqCommand(EntitySession session, SpecialCommandSubType subType, EntityKeyInfo key, LockType lockType,
                      List<EntityKeyMemberInfo> orderBy, object[] paramValues, Action<LinqCommand> setupAction)
                   : base(session, LinqCommandKind.Special, LinqOperation.Select) {
      SubType = subType; 
      Key = key;
      base.LockType = lockType;
      OrderBy = orderBy;
      ParamValues = paramValues;
      SetupAction = setupAction;
      this.SqlCacheKey = SqlCacheKeyBuilder.BuildSpecialSelectKey(subType, Key.Entity.Name, key.Name, lockType, orderBy);
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
