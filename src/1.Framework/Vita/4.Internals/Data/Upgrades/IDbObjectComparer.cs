using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Model;

namespace Vita.Data.Upgrades {

  public interface IDbObjectComparer {
    bool ColumnsMatch(DbColumnInfo oldColumn, DbColumnInfo newColumn, out string description);
    bool KeysMatch(DbKeyInfo oldKey, DbKeyInfo newKey);
    bool ViewsMatch(DbTableInfo oldView, DbTableInfo newView);
  }
}
