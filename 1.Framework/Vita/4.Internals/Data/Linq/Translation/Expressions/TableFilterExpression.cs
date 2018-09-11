using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Data.Linq.Translation.Expressions {
  public class TableFilterExpression: SqlExpression {
    public TableExpression Table;
    public DbTableFilter Filter;
    public List<ColumnExpression> Columns; 

    public TableFilterExpression(TableExpression table, EntityFilter filter)  : base(SqlExpressionType.TableFilter, typeof(bool)) {
      Table = table;
      Filter = new DbTableFilter() { EntityFilter = filter };
      // Retrieve columns
      Columns = new List<ColumnExpression>(); 
      foreach(var member in filter.Members) {
        var col = table.TableInfo.Columns.FirstOrDefault(c => c.Member == member);
        // that should never happen, but just in case
        Util.Check(col != null, "Error processing filter, column for member {0} not found. Entity: {1}.",
            member.MemberName, table.TableInfo.Entity.Name);
        Filter.Columns.Add(col); 
        Columns.Add(new ColumnExpression(table, col));
      }
    }

    public override Expression Mutate(System.Collections.Generic.IList<Expression> newOperands) {
      throw new Exception("Mutate not implemented for TableFilterExpression");
      /*
      if (newOperands != null && newOperands.Count > 0)
        Table = (TableExpression) newOperands[0];
      return this;
      */
    }

  }
}
