using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

using Vita.Data.Linq.Translation.Expressions;
using Vita.Entities;

namespace Vita.Data.Sql {

  public interface ISqlPrecedenceHandler {
    int GetPrecedence(ExpressionType type);
    int GetPrecedence(SqlFunctionType type);
    bool NeedsParenthesis(SqlFragment parent, SqlFragment child, bool isFirst);
  }

  public static class SqlPrecedence {
    public const int NoPrecedence = -1;
    public const int HighestPrecedence = 1000;
    public const int LowestPrecedence = 10;
  }

  public class SqlPrecedenceHandler : ISqlPrecedenceHandler {
    protected Dictionary<ExpressionType, int> ExpressionPrecedences = new Dictionary<ExpressionType, int>();
    protected Dictionary<SqlFunctionType, int> SqlFunctionPrecedences = new Dictionary<SqlFunctionType, int>();


    public SqlPrecedenceHandler() {
      SetupDefaultPrecedence(); 
    }

    #region ISqlPrecedenceHandler implementation
    public virtual int GetPrecedence(ExpressionType type) {
      if(ExpressionPrecedences.TryGetValue(type, out int result))
        return result;
      return SqlPrecedence.NoPrecedence;
    }

    public virtual int GetPrecedence(SqlFunctionType type) {
      if(SqlFunctionPrecedences.TryGetValue(type, out int result))
        return result;
      return SqlPrecedence.NoPrecedence;
    }

    public virtual bool NeedsParenthesis(SqlFragment parent, SqlFragment child, bool isFirst) {
      if(parent.Precedence == SqlPrecedence.NoPrecedence || child.Precedence == SqlPrecedence.NoPrecedence)
        return false;
      // we assume normal associativity (left to right)
      var result = isFirst ? child.Precedence < parent.Precedence : child.Precedence <= parent.Precedence;
      return result;
    }

    #endregion



    protected virtual void SetupDefaultPrecedence() {
      SetPrecedenceHighToLow(
        new[] { ExpressionType.Power },
        new[] { ExpressionType.Multiply, ExpressionType.MultiplyChecked, ExpressionType.Divide },
        new[] { ExpressionType.LeftShift, ExpressionType.RightShift },
        new[] { ExpressionType.Add, ExpressionType.AddChecked, ExpressionType.Subtract, ExpressionType.SubtractChecked },
        new[] { SqlFunctionType.OrBitwise, SqlFunctionType.AndBitwise, SqlFunctionType.XorBitwise },
        new[] { SqlFunctionType.IsNull, SqlFunctionType.IsNotNull },
        new[] { SqlFunctionType.Like },
        new[] { ExpressionType.LessThan, ExpressionType.LessThanOrEqual, ExpressionType.GreaterThan, ExpressionType.GreaterThanOrEqual },
        new[] { ExpressionType.Equal, ExpressionType.NotEqual },
        new[] { ExpressionType.And, ExpressionType.AndAlso },
        new[] { ExpressionType.Or, ExpressionType.OrElse, ExpressionType.ExclusiveOr },
        new[] { ExpressionType.Assign }
        );
    }

    protected void SetPrecedenceHighToLow(params object[] nodeTypes) {
      var currPrec = SqlPrecedence.HighestPrecedence;
      foreach(var nt in nodeTypes) {
        currPrec -= 10;
        switch(nt) {
          case ExpressionType et:
          case SqlFunctionType ft:
            SetPrecedenceValue(currPrec, nt);
            break;
          case ExpressionType[] etArr:
            foreach(var etEl in etArr)
              SetPrecedenceValue(currPrec, etEl);
            break;
          case SqlFunctionType[] ftArr:
            foreach(var ftEl in ftArr)
              SetPrecedenceValue(currPrec, ftEl);
            break;
          case object[] objArr:
            foreach(var obj in objArr)
              SetPrecedenceValue(currPrec, obj);
            break;
          default:
            Util.Throw("Invalid argument type for {0}.{1} method: must be ExpressionType or SQlFunctionType value, or an array of one of these types. Invalid value: {2}",
              this.GetType().Name, nameof(SetPrecedenceHighToLow), nt);
            break;
        }
      }
    }

    protected void SetPrecedenceValue(int prec, object nodeType) {
      switch(nodeType) {
        case ExpressionType et:
          ExpressionPrecedences[et] = prec;
          break;
        case SqlFunctionType ft:
          SqlFunctionPrecedences[ft] = prec;
          break;
        default:
          Util.Throw("Invalid argument type for {0}.{1} method: must be ExpressionType or SQlFunctionType value, or an array of one of these types. Invalid value: {2}",
            this.GetType().Name, nameof(SetPrecedenceHighToLow), nodeType);
          break;
      }
    }

  } //class
}
