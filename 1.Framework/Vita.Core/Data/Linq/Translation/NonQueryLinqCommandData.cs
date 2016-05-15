using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Entities.Linq;
using Vita.Entities.Model;

namespace Vita.Data.Linq.Translation {
  public class NonQueryLinqCommandData {
    public LinqCommand BaseLinqCommand;
    public SelectExpression BaseSelect;
    public List<Expression> SelectOutputValues = new List<Expression>();
    public TableExpression TargetTable;
    public bool IsSingleTableCommand;
    public List<ColumnExpression> TargetColumns = new List<ColumnExpression>();
    public List<Expression> Where = new List<Expression>();
    public List<TableExpression> From = new List<TableExpression>();

    public NonQueryLinqCommandData(LinqCommand baseLinqCommand, SelectExpression baseSelect, TableExpression targetTable, bool isSingleTable) {
      BaseLinqCommand = baseLinqCommand; 
      BaseSelect = baseSelect; 
      TargetTable = targetTable;
      IsSingleTableCommand = isSingleTable;
    }
    public LinqCommandType CommandType { get { return BaseLinqCommand.CommandType; } }
  }
}
