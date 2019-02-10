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
    // source properties, set at creation
    public LinqCommandSource Source; 
    public LinqOperation Operation;
    public EntityInfo UpdateEntity;
    public EntityMemberMask MemberMask;

    //analysis results
    public string SqlCacheKey;
    public List<Type> EntityTypes = new List<Type>();
    public List<LambdaExpression> Includes;
    public LockType LockType;
    public QueryOptions Options;
    public ParameterExpression[] ExternalParameters;

    // Rewriter results; can be set directly for prebuilt query 
    public LambdaExpression Lambda;
    public QueryResultShape ResultShape;


    public LinqCommand(LinqCommandSource source, LinqOperation op, EntityInfo updateEntity = null) {
      Source = source; 
      Operation = op;
      UpdateEntity = updateEntity; 
    }
  }

  public class DynamicLinqCommand : LinqCommand {
    public Expression Expression;
    public List<Expression> Locals = new List<Expression>();
    public object[] LocalValues;

    public DynamicLinqCommand(LinqCommandSource source, LinqOperation op, Expression expr, EntityInfo updateEntity = null)
                   :base(source, op, updateEntity)  {
      Expression = expr;
    }
  }

  public class ExecutableLinqCommand {
    public LinqCommand BaseCommand;
    public object[] ParamValues;

    public ExecutableLinqCommand(LinqCommand baseCommand) {
      BaseCommand = baseCommand; 
    }

    public ExecutableLinqCommand(LinqCommand command, object[] paramValues) {
      BaseCommand = command;
      ParamValues = paramValues; 
    }

  } //class

  public enum ParamValueSource {
    LocalVar, //from LocalValue
    Context, //from OperationContext filters
    Parameter, // explicit parameter in parameterized query
  }

  public class ParamValue {
    public ParameterExpression Parameter;
    public ParamValueSource ValueSource; 
    public object Value;
    public ParamValue(ParameterExpression parameter, ParamValueSource source, object value = null) {
      Parameter = parameter;
      ValueSource = source; 
      Value = value;
    }
  }



}
