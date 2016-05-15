using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Linq {

  public enum LinqCommandType {
    Select,
    Insert,
    Update,
    Delete,
  }

  public enum LinqCommandKind {
    DynamicSql,
    View,
    PrebuiltQuery,
  }

  public enum QueryResultShape {
    Entity,
    EntityList,
    Object
  }


  public class LinqCommand {
    public LinqCommandKind Kind;
    public LinqCommandType CommandType;
    public EntityQuery Query;
    public EntityInfo TargetEntity; //for delete, insert, update
    public LinqCommandInfo Info;
    public List<Expression> Locals;
    public object[] ParameterValues;

    public LinqCommand(EntityQuery query, LinqCommandType commandType, LinqCommandKind kind, EntityInfo targetEntity) {
      Kind = kind; 
      CommandType = commandType;
      Query = query;
      TargetEntity = targetEntity;
    }

    internal LinqCommand(LinqCommandInfo info, EntityInfo targetEntity, object[] parameterValues) {
      Info = info;
      Kind = info.CommandKind;
      CommandType = info.CommandType;
      TargetEntity = targetEntity;
      ParameterValues = parameterValues;
      Util.Check(ParameterValues.Length == info.Lambda.Parameters.Count, "Parameter values count ({0}) does not match parameter count ({1}) in lambda: {2}.",
          parameterValues.Length, Info.Lambda.Parameters.Count, Info.Lambda);
    }

    public override string ToString() {
      return string.Format("({0}) {1}", CommandType, QueryExpression);
    }

    public Type ResultType {
      get {
        if (Query != null)
          return Query.Expression.Type;
        else if (Info != null)
          return Info.Lambda.Body.Type;
        else
          return typeof(object); //unknown
      }
    }

    public Expression QueryExpression {
      get {
        if (Info != null)
          return Info.Lambda;
        if (Query != null)
          return Query.Expression;
        return null; 
      }
    }

    public void EvaluateLocalValues(EntitySession session) {
      Util.Check(session != null, "LINQ: Cannot evaluate query parameters, entity session not attached.");
      //We proceed in 2 steps: 
      // 1. We evaluate external parameters (used in lambdas in authorization filters and QueryFilters);
      //    values are in current OperationContext
      // 2. Evaluate local expressions which become final query parameters; they may depend on external params
      var extParamArray = Info.ExternalParameters.ToArray(); 
      var extParamValues = new object[extParamArray.Length];
      for (int i = 0; i < extParamArray.Length; i++)
          extParamValues[i] = session.EvaluateLambdaParameter(extParamArray[i]);
      //Evaluate local expressions
      ParameterValues = new object[Locals.Count];
      for (int i = 0; i < Locals.Count; i++ )
        ParameterValues[i] = ExpressionHelper.Evaluate(Locals[i], extParamArray, extParamValues);
    }


  }//class

  public enum CommandSchedule {
    TransactionStart,
    TransactionEnd,
  }

  public class ScheduledLinqCommand {
    public LinqCommand Command;
    public CommandSchedule Schedule;  
  }

}//ns