using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Linq;

namespace Vita.Data.Linq {

  public enum LinqCommandKind {
    Select,
    Insert,
    Update,
    Delete,
  }

  public class LinqCommand {
    public Expression Expression;

    public LinqCommandInfo Info;
    public List<Expression> Locals;
    public object[] ParameterValues;
    public readonly LinqCommandKind Kind;
    public EntityInfo TargetEntity;
    public bool IsView;

    //Non-query only
    public CommandSchedule Schedule; //Used only for scheduled commands

    public LinqCommand(Expression expr, LinqCommandKind kind, EntityInfo targetEntity, 
                             CommandSchedule schedule = CommandSchedule.TransactionEnd, bool isView = false) {
      Expression = expr;
      Kind = kind;
      TargetEntity = targetEntity;
      Schedule = schedule;
      IsView = isView; 
    }

    internal LinqCommand(LinqCommandInfo info, EntityInfo targetEntity, object[] parameterValues) {
      Info = info;
      Kind = LinqCommandKind.Select;
      TargetEntity = targetEntity;
      ParameterValues = parameterValues;
      Util.Check(ParameterValues.Length == info.Lambda.Parameters.Count, 
        "Parameter values count ({0}) does not match parameter count ({1}) in lambda: {2}.",
                 parameterValues.Length, Info.Lambda.Parameters.Count, Info.Lambda);
    }

    public override string ToString() {
      var expr = Info?.Lambda ?? Expression;
      return string.Format("({0}) {1}", Kind, expr);
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