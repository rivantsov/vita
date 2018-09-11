using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using Vita.Data.Runtime;
using Vita.Entities.Runtime;

namespace Vita.Data.SqlGen {

  public interface ISqlResultProcessor {
    object ProcessResult(DataCommand command);
  }

}
