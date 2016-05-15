using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Linq;
using System.Collections.ObjectModel;

using Vita.Common;
using Vita.Data.Driver;

namespace Vita.Data.Linq.Translation.Expressions {

  public interface IExecutableExpression {
    object Execute();
  }

  /// <summary>SQL specific custom expression types. </summary>
  public enum SqlFunctionType {
    IsNull = 100,
    IsNotNull,
    EqualNullables,
    Concat,
    Count,
    Exists,
    Like,
    Min,
    Max,
    Sum,
    Average,
    StringLength,
    ToUpper,
    ToLower,
    In,
    InArray,
    Substring,
    Trim,
    LTrim,
    RTrim,

    StringInsert,
    StringEqual,
    Replace,
    Remove,
    IndexOf,

    Year,
    Month,
    Day,
    Hour,
    Minute,
    Second,
    Millisecond,
    Now,
    Date,
    DateDiffInMilliseconds,
    Week,
    Time,

    Abs,
    Exp,
    Floor,
    Ln,
    Log,
    Pow,
    Round,
    Sign,
    Sqrt,

    //RI: 
    AndBitwise,
    OrBitwise,
    XorBitwise,
    ConvertBoolToBit,
    NewGuid
  }


  /// <summary>
  /// Holds new expression types (sql related), all well as their operands
  /// </summary>
  [DebuggerDisplay("SqlFunctionExpression {FunctionType}")]
  public class SqlFunctionExpression : OperandsMutableSqlExpression, IExecutableExpression
  {
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

      public object Execute()
      {
          switch (FunctionType) // SETuse
          {
              case SqlFunctionType.IsNull:
                  return Operands[0].Evaluate() == null;
              case SqlFunctionType.IsNotNull:
                  return Operands[0].Evaluate() != null;
              case SqlFunctionType.Concat:
                  {
                      var values = new List<string>();
                      foreach (var operand in Operands)
                      {
                          var value = operand.Evaluate();
                          if (value != null)
                              values.Add(System.Convert.ToString(value, CultureInfo.InvariantCulture));
                          else
                              values.Add(null);
                      }
                      return string.Concat(values.ToArray());
                  }
              case SqlFunctionType.Count:
                  {
                      var value = Operands[0].Evaluate();
                      // TODO: string is IEnumerable. See what we do here
                      if (value is IEnumerable)
                      {
                          int count = 0;
                          foreach (var dontCare in (IEnumerable)value)
                              count++;
                          return count;
                      }
                      // TODO: by default, shall we answer 1 or throw an exception?
                      return 1;
                  }
              case SqlFunctionType.Exists:
                  {
                      var value = Operands[0].Evaluate();
                      // TODO: string is IEnumerable. See what we do here
                      if (value is IEnumerable)
                      {
                          return true;
                      }
                      // TODO: by default, shall we answer 1 or throw an exception?
                      return false;
                  }
              case SqlFunctionType.Min:
                  {
                      decimal? min = null;
                      foreach (var operand in Operands)
                      {
                          var value = System.Convert.ToDecimal(operand.Evaluate());
                          if (!min.HasValue || value < min.Value)
                              min = value;
                      }
                      return System.Convert.ChangeType(min.Value, Operands[0].Type);
                  }
              case SqlFunctionType.Max:
                  {
                      decimal? max = null;
                      foreach (var operand in Operands)
                      {
                          var value = System.Convert.ToDecimal(operand.Evaluate());
                          if (!max.HasValue || value > max.Value)
                              max = value;
                      }
                      return System.Convert.ChangeType(max.Value, Operands[0].Type);
                  }
              case SqlFunctionType.Sum:
                  {
                      decimal sum = Operands.Select(op => System.Convert.ToDecimal(op.Evaluate())).Sum();
                      return System.Convert.ChangeType(sum, Operands.First().Type);
                  }
              case SqlFunctionType.Average:
                  {
                      decimal sum = 0;
                      foreach (var operand in Operands)
                          sum += System.Convert.ToDecimal(operand.Evaluate());
                      return sum / Operands.Count;
                  }
              case SqlFunctionType.StringLength:
                  return Operands[0].Evaluate().ToString().Length;
              case SqlFunctionType.ToUpper:
                  return Operands[0].Evaluate().ToString().ToUpper();
              case SqlFunctionType.ToLower:
                  return Operands[0].Evaluate().ToString().ToLower();
              case SqlFunctionType.Substring:
                  return EvaluateStandardCallInvoke("SubString", Operands);
              case SqlFunctionType.In:
                  throw new NotImplementedException();
              case SqlFunctionType.Replace:
                  return EvaluateStandardCallInvoke("Replace", Operands);
              case SqlFunctionType.Remove:
                  return EvaluateStandardCallInvoke("Remove", Operands);
              case SqlFunctionType.IndexOf:
                  return EvaluateStandardCallInvoke("IndexOf", Operands);
              case SqlFunctionType.Year:
                  return ((DateTime)Operands[0].Evaluate()).Year;
              case SqlFunctionType.Month:
                  return ((DateTime)Operands[0].Evaluate()).Month;
              case SqlFunctionType.Day:
                  return ((DateTime)Operands[0].Evaluate()).Day;
              case SqlFunctionType.Hour:
                  return ((DateTime)Operands[0].Evaluate()).Hour;
              case SqlFunctionType.Minute:
                  return ((DateTime)Operands[0].Evaluate()).Minute;
              case SqlFunctionType.Second:
                  return ((DateTime)Operands[0].Evaluate()).Second;
              case SqlFunctionType.Millisecond:
                  return ((DateTime)Operands[0].Evaluate()).Millisecond;
              case SqlFunctionType.Now:
                  return DateTime.Now;
              case SqlFunctionType.Date:
                  return ((DateTime)Operands[0].Evaluate());
              case SqlFunctionType.DateDiffInMilliseconds:
                  return ((DateTime)Operands[0].Evaluate()) - ((DateTime)Operands[1].Evaluate());
              case SqlFunctionType.Abs:
              case SqlFunctionType.Exp:
              case SqlFunctionType.Floor:
              case SqlFunctionType.Ln:
              case SqlFunctionType.Log:
              case SqlFunctionType.Pow:
              case SqlFunctionType.Round:
              case SqlFunctionType.Sign:
              case SqlFunctionType.Sqrt:
                  return EvaluateMathCallInvoke(FunctionType, Operands);
              default:
                  Util.Throw("S0116: Unknown SpecialExpressionType ({0})", FunctionType);
                  return null; 
          }
      }

      private object EvaluateMathCallInvoke(SqlFunctionType SpecialNodeType, IList<Expression> operands)
      {
          return typeof(Math).GetMethod(SpecialNodeType.ToString(), operands.Skip(1).Select(op => op.Type).ToArray())
                  .Invoke(null, operands.Skip(1).Select(op => op.Evaluate()).ToArray());
      }
      protected object EvaluateStatardMemberAccess(string propertyName, IList<Expression> operands)
      {
          return operands[0].Type.GetProperty(propertyName).GetValue(operands.First().Evaluate(), null);
      }
      protected object EvaluateStandardCallInvoke(string methodName, IList<Expression> operands)
      {
          return operands[0].Type.GetMethod(methodName,
                                      operands.Skip(1).Select(op => op.Type).ToArray())
                                      .Invoke(operands[0].Evaluate(),
                                              operands.Skip(1).Select(op => op.Evaluate()).ToArray());
      }
  }
}