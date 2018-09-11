using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Linq;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq.Translation {
  public class NonQueryLinqCommandData {
    public EntityCommand BaseLinqCommand;
    public SelectExpression BaseSelect;
    public List<Expression> SelectOutputValues = new List<Expression>();
    public TableExpression TargetTable;
    public bool UseSimpleCommand;
    public List<ColumnExpression> TargetColumns = new List<ColumnExpression>();

    public NonQueryLinqCommandData(EntityCommand baseLinqCommand, SelectExpression baseSelect, TableExpression targetTable) {
      BaseLinqCommand = baseLinqCommand; 
      BaseSelect = baseSelect; 
      TargetTable = targetTable;
      switch(BaseLinqCommand.Operation) {
        case EntityOperation.Insert: UseSimpleCommand = false; break;
        default:
          var allTables = BaseSelect.Tables;
          var usesSkipTakeOrderBy = BaseSelect.Offset != null || BaseSelect.Limit != null; 
          UseSimpleCommand = allTables.Count == 1 && allTables[0].TableInfo == targetTable.TableInfo && !usesSkipTakeOrderBy;
          break; 

      }

    }
    public EntityOperation Operation { get { return BaseLinqCommand.Operation; } }
  }
}
