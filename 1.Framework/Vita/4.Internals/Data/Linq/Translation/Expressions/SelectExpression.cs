
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


    public Expression Where;
    public IList<Expression> Having = new List<Expression>();
    public IList<OrderByExpression> OrderBy = new List<OrderByExpression>();
    public IList<GroupExpression> Group = new List<GroupExpression>();

    public Expression Offset { get; set; }
    public Expression Limit { get; set; }
    public SelectExpressionFlags Flags; //MS SQL Only

    public MultiSetChainLink ChainedSet; // Unioned set

    public RowListProcessor RowListProcessor { get; set; }
    public LambdaExpression RowReaderLambda { get; set; } // Func<IDataRecord,EntitySession,T> --> creates an object from data record
    public EntityRecordReader EntityReader; //for tables/entities we avoid going thru lambda/compile
    public Type ReaderOutputType {
      get { return RowReaderLambda?.Body.Type ?? EntityReader.EntityType; } 
    }

    // Parent scope: we will climb up to find if we don't find the request table in the current scope
    public SelectExpression Parent { get; set; }

    public LinqCommand Command;

    public SelectExpression(LinqCommand command) : base(SqlExpressionType.Select, typeof(object), null) {
      Command = command; 
    }

    public SelectExpression(SelectExpression parentSelectExpression, LinqCommand queryInfo) 
         : base(SqlExpressionType.Select, null, null) {
      Parent = parentSelectExpression;
      Command = queryInfo; 
    }

    private SelectExpression(LinqCommand command, Type type, IList<Expression> operands) : base(SqlExpressionType.Select, type, operands) {
      Command = command; 
    }

    protected override Expression Mutate2(IList<Expression> newOperands) {
      Type type;
      if(newOperands.Count > 0)
        type = newOperands[0].Type;
      else
        type = Type;
      var newSelect = new SelectExpression(Command, type, newOperands);
      newSelect.Tables = Tables;
      newSelect.Columns = Columns;
      newSelect.Where = Where;
      newSelect.OrderBy = OrderBy;
      newSelect.Group = Group;
      newSelect.Parent = Parent;
      newSelect.RowListProcessor = RowListProcessor;
      newSelect.RowReaderLambda = RowReaderLambda;
      newSelect.EntityReader = EntityReader;
      newSelect.Limit = Limit;
      newSelect.Offset = Offset;
      newSelect.ChainedSet = ChainedSet; 
      return newSelect;
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