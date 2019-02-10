using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;

namespace Vita.Data.Linq {

  // TODO: See if this class is needed at all
  public class NonQueryLinqCommand {
    public LinqCommand BaseLinqCommand;
    public DbTableInfo TargetTable;
    public List<DbColumnInfo> TargetColumns = new List<DbColumnInfo>();

    // TODO: move these to parameter of DbLinqNonQuerySqlBuilder
    public SelectExpression BaseSelect;
    public List<Expression> SelectOutputValues = new List<Expression>();
    public bool UseSimpleCommand;

    public NonQueryLinqCommand(LinqCommand baseLinqCommand, DbTableInfo targetTable, SelectExpression baseSelect) {
      BaseLinqCommand = baseLinqCommand; 
      BaseSelect = baseSelect; 
      TargetTable = targetTable;
    }

    public LinqOperation Operation { get { return BaseLinqCommand.Operation; } }
  } //class
}
