using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Linq;

namespace Vita.Entities.Runtime {

  public enum EntityOperation {
    Select,
    Insert,
    Update,
    Delete,
  }

  public class EntityCommand {
    public Expression Expression;

    public QueryInfo Info;
    public List<Expression> Locals;
    public object[] ParameterValues;
    public readonly EntityOperation Operation;
    public EntityInfo TargetEntity;
    public bool IsView;

    //Non-query only
    public CommandSchedule Schedule; //Used only for scheduled commands

    public EntityCommand(Expression expr, EntityOperation operation, EntityInfo targetEntity, 
                             CommandSchedule schedule = CommandSchedule.TransactionEnd, bool isView = false) {
      Expression = expr;
      Operation = operation;
      TargetEntity = targetEntity;
      Schedule = schedule;
      IsView = isView; 
    }

    internal EntityCommand(QueryInfo info, EntityInfo targetEntity, object[] parameterValues) {
      Info = info;
      Operation = EntityOperation.Select;
      TargetEntity = targetEntity;
      ParameterValues = parameterValues;
      Util.Check(ParameterValues.Length == info.Lambda.Parameters.Count, 
        "Parameter values count ({0}) does not match parameter count ({1}) in lambda: {2}.",
                 parameterValues.Length, Info.Lambda.Parameters.Count, Info.Lambda);
    }

    public override string ToString() {
      return string.Format("({0}) {1}", Operation, QueryExpression);
    }

    public Type ResultType {
      get {
        if (Expression != null)
          return Expression.Type;
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
        return Expression;
      }
    }



  }//class

}//ns