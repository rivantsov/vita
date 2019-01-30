using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Vita.Data.Linq.Translation.Expressions;

namespace Vita.Data.Linq {

  public class NonQueryLinqCommand {
    public LinqCommand BaseLinqCommand;
    public SelectExpression BaseSelect;
    public List<Expression> SelectOutputValues = new List<Expression>();
    public TableExpression TargetTable;
    public bool UseSimpleCommand;
    public List<ColumnExpression> TargetColumns = new List<ColumnExpression>();

    public NonQueryLinqCommand(LinqCommand baseLinqCommand, SelectExpression baseSelect, TableExpression targetTable) {
      BaseLinqCommand = baseLinqCommand; 
      BaseSelect = baseSelect; 
      TargetTable = targetTable;
      switch(BaseLinqCommand.Kind) {
        case LinqCommandKind.Insert: UseSimpleCommand = false; break;
        default:
          var allTables = BaseSelect.Tables;
          var usesSkipTakeOrderBy = BaseSelect.Offset != null || BaseSelect.Limit != null; 
          UseSimpleCommand = allTables.Count == 1 && allTables[0].TableInfo == targetTable.TableInfo && !usesSkipTakeOrderBy;
          break; 
      }

    }
    public LinqCommandKind CommandKind { get { return BaseLinqCommand.Kind; } }
  }
}
