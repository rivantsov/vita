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
    public bool UseSimpleCommand;
    public List<ColumnExpression> TargetColumns = new List<ColumnExpression>();

    public NonQueryLinqCommandData(LinqCommand baseLinqCommand, SelectExpression baseSelect, TableExpression targetTable) {
      BaseLinqCommand = baseLinqCommand; 
      BaseSelect = baseSelect; 
      TargetTable = targetTable;
      switch(BaseLinqCommand.CommandType) {
        case LinqCommandType.Insert: UseSimpleCommand = false; break;
        default:
          var allTables = BaseSelect.Tables;
          var usesSkipTakeOrderBy = BaseSelect.Offset != null || BaseSelect.Limit != null; 
          UseSimpleCommand = allTables.Count == 1 && allTables[0].TableInfo == targetTable.TableInfo && !usesSkipTakeOrderBy;
          break; 

      }

    }
    public LinqCommandType CommandType { get { return BaseLinqCommand.CommandType; } }
  }
}
