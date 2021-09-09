using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Data.Upgrades {

  public class DbUpgradeSettings {
    public DbUpgradeMode Mode = DbUpgradeMode.NonProductionOnly;
    public DbUpgradeOptions Options = DbUpgradeOptions.Default;
    public HashSet<string> IgnoreDbObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  }

}
