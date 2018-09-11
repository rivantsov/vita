using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Data.Driver;
using Vita.Entities;

namespace Vita.Data.Model {

  /// <summary>Holds DB model settings.</summary>
  /// <remarks>The purpose of this class is to hold DB model settings that are not tied to particular 
  /// database instance. It also holds an instance of DB model that can be shared between database 
  /// instances. Typical use - multi-tenant scenario with separate database for each tenant.
  /// </remarks>
  public class DbModelConfig {
    public readonly DbDriver Driver;
    public readonly DbOptions Options;
    // areaname => schema name
    public readonly Dictionary<string, string> SchemaMappings 
             = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IDbNamingPolicy NamingPolicy;
    public string DbViewPrefix = "v"; 
    public DbModel SharedDbModel; 

    public DbModelConfig(DbDriver driver, DbOptions options, IDbNamingPolicy namingPolicy = null,
              IDictionary<string, string> schemaMappings = null) {
      Util.Check(driver != null, "Driver parameter may not be null.");
      Util.Check(driver.TypeRegistry != null, 
        "DbDriver.TypRegistry is null, DbDriver-derived class must create driver-specific instance.");
      Util.Check(driver.SqlDialect != null,
        "DbDriver.SqlDialect is null, DbDriver-derived class must create driver-specific instance.");
      Driver = driver;
      Options = options;
      NamingPolicy = namingPolicy; 
      //import schema mappings
      if (schemaMappings != null)
        foreach (var de in schemaMappings)
          SchemaMappings[de.Key] = de.Value; 
    }

    public void MapSchema(string areaName, string schema) {
      SchemaMappings[areaName] = schema; 
    }

    public string GetSchema(EntityArea area) {
      return GetSchema(area.Name); 
    }

    public string GetSchema(string areaName) {
      string schema;
      if(SchemaMappings.TryGetValue(areaName, out schema))
        return schema;
      return areaName;
    }

  } //class
} //ns
