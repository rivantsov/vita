using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using Vita.Data.Sql;
using Vita.Entities.Runtime;

namespace Vita.Data.Runtime {


  public class DbBatch {
    public DbUpdateSet UpdateSet;
    public List<DataCommand> Commands = new List<DataCommand>();
  }
}
