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

  /// <summary>
  /// Holds new expression types (sql related), all well as their operands
  /// </summary>
  [DebuggerDisplay("SqlFunctionExpression {FunctionType}")]
  public class SqlFunctionExpression : OperandsMutableSqlExpression  {
      public readonly SqlFunctionType FunctionType;
      public readonly bool ForceIgnoreCase;

      public SqlFunctionExpression(SqlFunctionType functionType, Type type, params Expression[] operands)
          : this (functionType, type, true, operands) {
      }
      public SqlFunctionExpression(SqlFunctionType functionType, Type type, bool ignoreCase, params Expression[] operands)
                   : base(SqlExpressionType.SqlFunction, type, operands) {
          this.FunctionType = functionType;
          this.ForceIgnoreCase = ignoreCase; 
      }

      protected override Expression Mutate2(IList<Expression> newOperands)
      {
          return new SqlFunctionExpression(this.FunctionType, this.Type, this.ForceIgnoreCase, newOperands.ToArray());
      }

  }
}