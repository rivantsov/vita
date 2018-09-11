
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Vita.Data.SqlGen;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Data.Linq.Translation.Expressions {

  public abstract class SqlExpression : Expression, IMutableExpression
  {
    public SqlExpressionType SqlNodeType;
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
    public bool HasAlias() {
      return !string.IsNullOrEmpty(Alias);
    }
    public SqlFragment GetAliasSql() {
      if(!HasAlias())
        return null;
      return _aliasSql = _aliasSql ?? new TextSqlFragment(this.Alias); 
    } SqlFragment _aliasSql; 

    public virtual Expression Mutate(IList<Expression> newOperands) { 
        if (newOperands.Count > 0)
            Util.Throw("Default MutableExpression does not allow operands");
        return this;
    }
  }
}