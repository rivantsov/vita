using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data.Driver;
using Vita.Entities;

namespace Vita.Data.Model {

  public class DbModelConfig {
    public readonly DbDriver Driver;
    public readonly DbOptions Options;
    public readonly DbNamingPolicy NamingPolicy;
    // areaname => schema name
    public readonly Dictionary<string, string> SchemaMappings 
             = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    internal DbModel SharedDbModel; 

    public DbModelConfig(DbDriver driver, DbOptions options = DbOptions.Default, 
         DbNamingPolicy namingPolicy = null, IDictionary<string, string> schemaMappings = null) {
      Driver = driver;
      Options = options;
      NamingPolicy = namingPolicy ?? new DbNamingPolicy();
      //import schema mappings
      if (schemaMappings != null)
        foreach (var de in schemaMappings)
          SchemaMappings[de.Key] = de.Value; 
      
      //Verify options
      if (!Driver.Supports(DbFeatures.StoredProcedures))
        Options &= ~DbOptions.UseStoredProcs;
      //Batch mode is not available without stored procedures
      if (!Options.IsSet(DbOptions.UseStoredProcs))
        Options &= ~DbOptions.UseBatchMode;
    }

    public void MapSchema(string areaName, string schema) {
      SchemaMappings[areaName] = schema; 
    }

    public string GetSchema(EntityArea area) {
      string schema;
      if (SchemaMappings.TryGetValue(area.Name, out schema))
        return schema; 
      return area.Name; 
    }

    /*
          // Initialize modelConfig.AllSchemas set
          if (_config.AllSchemas.Count == 0)
            foreach (var area in _entityModel.App.Areas)
              _config.AllSchemas.Add(_config.GetSchema(area.Name));
     */

  } //class

} //ns
