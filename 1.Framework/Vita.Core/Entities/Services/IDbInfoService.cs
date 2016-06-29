using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data;

namespace Vita.Entities.Services {

  public interface IDbInfoService {
    DbVersionInfo LoadDbInfo(DbSettings settings, string appName, Vita.Data.Driver.DbModelLoader loader);
    bool UpdateDbInfo(Database database, Exception exception = null);
  }

}
