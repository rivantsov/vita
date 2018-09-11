using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Linq;

namespace Vita.Data.SqlGen {

  public interface IFlatSqlFragment {
    void AddFormatted(IList<string> strings, IList<string> placeHolderArgs);
  }
}
