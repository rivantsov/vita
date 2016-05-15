
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using Vita.Common;

namespace Vita.Data.Linq.Translation.Expressions {

  /// <summary>Special Expression types for Sql expressions. </summary>
  public enum SqlExpressionType {
    Select,  
    MetaTable,
    Table,
    Column,
    ExternalValue, // Query parameter or value derived from it
    OrderBy,
    Group,
    StartIndexOffset,
    Alias, 
    NonQueryCommand, // for update statements
    SqlFunction, //SQL function or operator
    TableFilter, //List filter
  }

  public abstract class SqlExpression : Expression, IMutableExpression
  {
    public readonly SqlExpressionType SqlNodeType;
    public string Alias;
    public virtual List<Expression> Operands { get; private set; }


    // Note: constructor Expression(exprType, Type) is obsolete: the recommended way by MS is to override NodeType and Type virtual properties
    // For custom expressions NodeType should always return Extension
    public override ExpressionType NodeType {
      get { return ExpressionType.Extension; }
    }
    public override Type Type {
      get { return _type; }
    } Type _type; 

    protected SqlExpression(SqlExpressionType sqlNodeType, Type type): base() {
      SqlNodeType = sqlNodeType;
      _type = type;
      Operands = new List<Expression>(); 
    }

    public virtual Expression Mutate(IList<Expression> newOperands) { 
        if (newOperands.Count > 0)
            Util.Throw("Default MutableExpression does not allow operands");
        return this;
    }
  }
}