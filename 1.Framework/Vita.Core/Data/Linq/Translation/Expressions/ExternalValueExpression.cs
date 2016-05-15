
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Common;

namespace Vita.Data.Linq.Translation.Expressions {

    public enum ExternalValueSqlUse {
      NotUsed = 0,
      Parameter = 1,
      Literal = 3,
    }

    /// <summary>Represents external value - query parameter or value derived from it.</summary>
    public class ExternalValueExpression : SqlExpression
    {
        public Expression SourceExpression;
        // Counts # of times the value is used in expression directly, not in derived form, as part of client-side expression
        // - in which case the derived value becomes a real SQL parameter
        // Only parameters with SqlUseCount > 0 are real dbParameters
        public int SqlUseCount;

        public ExternalValueSqlUse SqlUse;
        public Type ListElementType; 

        public LinqCommandParameter LinqParameter; // asigned only for real db parameters
        public object LiteralValue; // if Usage==Literal

        //public string Alias { get; set; }

        public ExternalValueExpression(Expression sourceExpression)   : base(SqlExpressionType.ExternalValue, sourceExpression.Type) {
          SourceExpression = sourceExpression;
          SqlUse = ExternalValueSqlUse.NotUsed; //default
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