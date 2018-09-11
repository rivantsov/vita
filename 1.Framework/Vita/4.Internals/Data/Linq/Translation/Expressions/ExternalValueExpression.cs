
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Vita.Data.SqlGen;
using Vita.Entities.Utilities;

namespace Vita.Data.Linq.Translation.Expressions {

  /*
  public enum SqlValueMode {
    Parameter,
    Literal,
  }
  */
  /// <summary>Represents external value - query parameter or value derived from it.</summary>
  public class ExternalValueExpression : SqlExpression {
    public Expression SourceExpression;
    // Counts # of times the value is used in expression directly, not in derived form, as part of client-side expression
    // - in which case the derived value becomes a real SQL parameter
    // Only parameters with SqlUseCount > 0 are real dbParameters
    public int SqlUseCount;

    // public SqlValueMode SqlMode;
    public Type ListElementType;

    public SqlPlaceHolder SqlPlaceHolder;

    //public string Alias { get; set; }

    public ExternalValueExpression(Expression sourceExpression) : base(SqlExpressionType.ExternalValue, sourceExpression.Type) {
      SourceExpression = sourceExpression;
      //check if it is list parameter
      var type = sourceExpression.Type;
      if (type.IsListOrArray()) {
        if (type.IsArray)
          ListElementType = type.GetElementType();
        else
          ListElementType = type.GetGenericArguments()[0];
      }
    }

    public bool IsList {
      get { return ListElementType != null; }
    }

    public override string ToString() {
      return "P:" + SourceExpression.ToString();
    }
  }

}