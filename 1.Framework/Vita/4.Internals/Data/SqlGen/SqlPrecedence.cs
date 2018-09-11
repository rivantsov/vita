using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Data.SqlGen {

  public static class SqlPrecedence {
    public const int NoPrecedence = -1;
    public const int HighestPrecedence = 1000;
    public const int LowestPrecedence = 10;

  }
}
