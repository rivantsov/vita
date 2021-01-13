using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Services;

namespace Vita.Data {

  public class DbSettings {
    public readonly string DataSourceName;
    public readonly DbModelConfig ModelConfig;
    public readonly DbUpgradeMode UpgradeMode;
    public DbUpgradeOptions UpgradeOptions;
    public Dictionary<string, string> CustomSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); 

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
              upgradeMode, upgradeOptions, dataSourceName)   {  }

    // Use this constructor for shared db model (multi-tenant app aganst multiple identical databases)
    public DbSettings(DbModelConfig modelConfig, 
                      string connectionString, 
                      string schemaManagementConnectionString = null, 
                      DbUpgradeMode upgradeMode = DbUpgradeMode.NonProductionOnly, 
                      DbUpgradeOptions upgradeOptions = DbUpgradeOptions.Default,
                      string dataSourceName = "(Default)") {
      ModelConfig = modelConfig;
      ConnectionString = connectionString;
      SchemaManagementConnectionString = schemaManagementConnectionString ?? connectionString;
      UpgradeMode = upgradeMode;
      UpgradeOptions = upgradeOptions;
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
