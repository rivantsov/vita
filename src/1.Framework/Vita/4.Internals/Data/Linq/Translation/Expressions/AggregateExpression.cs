using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection; 

using Vita.Entities.Utilities;
using Vita.Data.Driver;
using Vita.Entities;

namespace Vita.Data.Linq.Translation.Expressions {

  public class AggregateExpression : OperandsMutableSqlExpression   {
      public readonly AggregateType AggregateType;

      public AggregateExpression(AggregateType aggregateType, Type type, Expression[] operands) : base(SqlExpressionType.Aggregate, type, operands) { 
          this.AggregateType = aggregateType;
      }

      protected override Expression Mutate2(IList<Expression> newOperands)
      {
          return new AggregateExpression(this.AggregateType, this.Type, newOperands.ToArray());
      }

  }
}