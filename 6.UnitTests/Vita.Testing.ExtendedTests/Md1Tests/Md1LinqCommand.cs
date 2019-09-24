using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

using Vita.Data.Linq;
using Vita.Entities.Runtime;

namespace Vita.Entities.MetaD1 {

  using Vita.Data.Linq.Translation.Expressions;

  public class Md1LinqCommand: LinqCommand {

    public ViewQuery Query;

    public Md1LinqCommand(IEntitySession session, ViewQuery query, SelectExpression select)
            : base ((EntitySession)session, LinqCommandKind.View, LinqOperation.Select) {
      Query = query;
      SelectExpression = select;
      SelectExpression.Command = this; 
    }
  }
}
