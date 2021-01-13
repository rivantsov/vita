using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities;

namespace Vita.Entities {

  public static class NativeSqlFunctionStubs {

    public static long NextValue(this SequenceDefinition sequence) {
      Util.Throw("Function may not be called directly, only referenced in LINQ expressions.");
      return 0; // never happens 
    }

  }
}
