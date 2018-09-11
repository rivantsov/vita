using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data;
using Vita.Data.Model;
using Vita.Entities.Logging;
using Vita.Entities.Model;

namespace Vita.Entities.DbInfo {

  public interface IDbInfoService {
    DbVersionInfo LoadDbVersionInfo(DbModel dbModel, DbSettings settings, IActivationLog log);
    bool UpdateDbInfo(DbModel dbModel, DbSettings settings, IActivationLog log, Exception exception = null);
  }

}
