using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Sql;

namespace Vita.Data.Linq {

  // TODO: See if this class is needed at all
  public class NonQueryLinqCommand {
    public LinqCommand BaseLinqCommand;
    public List<DbColumnInfo> TargetColumns = new List<DbColumnInfo>();

    // TODO: move these to parameter of DbLinqNonQuerySqlBuilder
    public SelectExpression BaseSelect;
    public List<Expression> SelectOutputValues = new List<Expression>();
    public bool UseSimpleCommand;

    public DbTableInfo TargetTable;
    public TextSqlFragment TargetTableSqlFullName => TargetTable.SqlFullName;
    public LinqOperation Operation => BaseLinqCommand.Operation;

    public NonQueryLinqCommand(LinqCommand baseLinqCommand, DbTableInfo targetTable, SelectExpression baseSelect) {
      BaseLinqCommand = baseLinqCommand; 
      BaseSelect = baseSelect; 
      TargetTable = targetTable;
    }

  } //class
}
