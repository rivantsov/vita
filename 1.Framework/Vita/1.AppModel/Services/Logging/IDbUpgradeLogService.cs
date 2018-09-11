using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Upgrades;

namespace Vita.Entities.Services {

  public class DbUpgradeReport {
    public Version Version; 
    public Version OldDbVersion;
    public DbUpgradeMethod Method;
    public string MachineName;
    public string UserName;
    public DateTime StartedOn;
    public DateTime? CompletedOn;
    public List<DbUpgradeScript> Scripts;
    public DbUpgradeScript FailedScript;
    public Exception Exception;
  }


  public interface IDbUpgradeLogService{
    void LogDbUpgrade(DbUpgradeReport report);
  }

}
