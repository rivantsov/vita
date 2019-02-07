using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq {

  // TODO: move here LinqNonQueryCommand

  public enum LinqOperation {
    Select,
    Insert,
    Update,
    Delete,
  }

  public enum LinqCommandSource {
    PrebuiltQuery,
    View,
    Dynamic,
  }

  public class LinqCommand {
    public LinqCommandSource Source; 
    public LinqOperation Operation;
    public Expression Expression; 
    public LambdaExpression Lambda;

    public EntityInfo UpdateEntity; 
    public EntityMemberMask MemberMask;
    public QueryResultShape ResultShape;

    //derived values
    public string CacheKey;
    public List<LambdaExpression> Includes;
    public LockType LockType;
    public QueryOptions Options;
    public List<Expression> Locals;
    public List<EntityInfo> Entities = new List<EntityInfo>(); 

    public LinqCommand(LinqCommandSource source, LinqOperation op, Expression expr, EntityInfo updateEntity = null) {
      Source = source; 
      Operation = op;
      Expression = expr;
      UpdateEntity = updateEntity; 
    }
  }

  public sealed class ExecutableLinqCommand {
    public LinqCommand BaseCommand;
    public List<InputValue> InputValues = new List<InputValue>();

    public ExecutableLinqCommand(LinqCommandSource source, LinqOperation op, Expression expression, EntityInfo updateEntity = null) {
      BaseCommand = new LinqCommand(source, op, expression, updateEntity);
    }

    public ExecutableLinqCommand(LinqCommand command) {
      BaseCommand = command; 
    }



  } //class

  public class InputValue {
    public Expression Expression;
    public object Value;
    public InputValue(Expression expr, object value = null) {
      Expression = expr;
      Value = value;
    }
  }



}
