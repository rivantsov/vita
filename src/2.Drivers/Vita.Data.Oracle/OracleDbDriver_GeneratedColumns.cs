using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Data.Oracle {

  // Oracle does not allow reading back the value of expression for computed (generated column)
  // The meta field is there but its type LONG - deprecated type, and there is no way to read 
  // the value using Oracle .NET provider. As a result, there is no way to compare current expr
  // for a column in DB vs the (new) value in attribute on entity property. 
  // So we have no choice but to either always recreate the column, or never do it. 
  // The default is NEVER. 

  public enum GeneratedColumnUpdateMode {
    Never, 
    Always
  }

  partial class OracleDbDriver {
    public static GeneratedColumnUpdateMode GeneratedColumnUpdateMode = GeneratedColumnUpdateMode.Never;
  }
}
