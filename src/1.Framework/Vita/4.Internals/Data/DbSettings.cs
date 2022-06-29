using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Services;
using Vita.Data.Upgrades;

namespace Vita.Data {

  public class DbSettings {
    public readonly string DataSourceName;
    public readonly DbModelConfig ModelConfig;
    public DbUpgradeSettings UpgradeSettings; 
    public Dictionary<string, string> CustomSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public DbUpgradeMode UpgradeMode => UpgradeSettings.Mode;
    public DbUpgradeOptions UpgradeOptions => UpgradeSettings.Options;

    // Connection strings
    public readonly string ConnectionString;
    public readonly string SchemaManagementConnectionString; //optional, admin-privilege conn string

    public DbSettings(DbDriver driver, DbOptions options, 
                      string connectionString, 
                      string schemaManagementConnectionString = null, 
                      DbUpgradeMode upgradeMode = DbUpgradeMode.NonProductionOnly, 
                      DbUpgradeOptions upgradeOptions = DbUpgradeOptions.Default,
                      IDbNamingPolicy namingPolicy = null,
                      string dataSourceName = "(Default)") 
       : this (new DbModelConfig(driver, options, namingPolicy), connectionString, schemaManagementConnectionString, 
           new DbUpgradeSettings() { Mode = upgradeMode, Options = upgradeOptions}, dataSourceName)   {  }

    // Use this constructor for shared db model (multi-tenant app aganst multiple identical databases)
    public DbSettings(DbModelConfig modelConfig, 
                      string connectionString, 
                      string schemaManagementConnectionString = null, 
                      DbUpgradeSettings upgradeSettings = null, 
                      string dataSourceName = "(Default)") {
      ModelConfig = modelConfig;
      ConnectionString = connectionString;
      SchemaManagementConnectionString = schemaManagementConnectionString ?? connectionString;
      this.UpgradeSettings = upgradeSettings ?? new DbUpgradeSettings();
      DataSourceName = dataSourceName;
    }

    public DbDriver Driver {
      get { return ModelConfig.Driver; }
    }

    public string GetCustomSetting(string key, string defaultValue = null) {
      if(CustomSettings.TryGetValue(key, out string value))
        return value;
      return defaultValue; 
    }
  }//class


}
