
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq.Translation.Expressions {

  /// <summary>
  /// ScopeExpression describes a selection.
  /// It can be present at top-level or as subexpressions
  /// </summary>
  public class SelectExpression : OperandsMutableSqlExpression {
    // Involved entities
    public IList<TableExpression> Tables = new List<TableExpression>();
    public IList<ColumnExpression> Columns = new List<ColumnExpression>(); 


    public IList<Expression> Where = new List<Expression>();
    public IList<Expression> Having = new List<Expression>();
    public IList<OrderByExpression> OrderBy = new List<OrderByExpression>();
    public IList<GroupExpression> Group = new List<GroupExpression>();

    public Expression Offset { get; set; }
    public Expression Limit { get; set; }
    public SelectExpressionFlags Flags; //MS SQL Only

    // Clauses
    public RowListProcessor RowListProcessor { get; set; }
    public LambdaExpression RowReaderLambda { get; set; } // Func<IDataRecord,EntitySession,T> --> creates an object from data record
    public EntityRecordReader EntityReader; //for tables/entities we avoid going thru lambda/compile
    public Type ReaderOutputType {
      get { return RowReaderLambda?.Body.Type ?? EntityReader.EntityType; } 
    }

    // Parent scope: we will climb up to find if we don't find the request table in the current scope
    public SelectExpression Parent { get; set; }

    public LinqCommandInfo CommandInfo;

    public SelectExpression(LinqCommandInfo info) : base(SqlExpressionType.Select, null, null) {
      CommandInfo = info; 
    }

    public SelectExpression(SelectExpression parentSelectExpression, LinqCommandInfo queryInfo) 
         : base(SqlExpressionType.Select, null, null) {
      Parent = parentSelectExpression;
      CommandInfo = queryInfo; 
    }

    private SelectExpression(LinqCommandInfo info, Type type, IList<Expression> operands) : base(SqlExpressionType.Select, type, operands) {
      CommandInfo = info; 
    }

    protected override Expression Mutate2(IList<Expression> newOperands) {
      Type type;
      if(newOperands.Count > 0)
        type = newOperands[0].Type;
      else
        type = Type;
      var scopeExpression = new SelectExpression(CommandInfo, type, newOperands);
      scopeExpression.Tables = Tables;
      scopeExpression.Columns = Columns;
      scopeExpression.Where = Where;
      scopeExpression.OrderBy = OrderBy;
      scopeExpression.Group = Group;
      scopeExpression.Parent = Parent;
      scopeExpression.RowListProcessor = RowListProcessor;
      scopeExpression.RowReaderLambda = RowReaderLambda;
      scopeExpression.EntityReader = EntityReader;
      scopeExpression.Limit = Limit;
      scopeExpression.Offset = Offset;
      return scopeExpression;
    }

    //helper methods
    public bool HasOutAggregates() {
      var result = this.Operands.OfType<AggregateExpression>().Any();
      return result;
    }
    public bool HasOrderBy() {
      return OrderBy.Count > 0;
    }
    public bool HasLimit() {
      return Limit != null;
    }

  }//class
}