using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Linq;

using Vita.Data.Model;

namespace Vita.Data.Linq.Translation.Expressions {

  [DebuggerDisplay("Sql: {Snippet}")]
  public class CustomSqlExpression : OperandsMutableSqlExpression  {
      public readonly CustomSqlSnippet Snippet;

      public CustomSqlExpression(CustomSqlSnippet snippet, IList<Expression> operands)
          :base(SqlExpressionType.CustomSqlSnippet, snippet.Method.ReturnType, operands)
      {
          this.Snippet = snippet;   
      }

      protected override Expression Mutate2(IList<Expression> newOperands)
      {
          return new CustomSqlExpression(this.Snippet, newOperands);
      }

  }
}