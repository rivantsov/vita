using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Model;
using Vita.Entities;

namespace Vita.Data {

  public enum DbInstanceType {
    // Dev instance on dev machine, not shared, allow schema update any time directly from the app
    Development = 0,
    // Shared test/staging version; software installed through centralized deployment process
    Staging = 1,
    // Production database, schema update allowed only through db tool by system admin
    Production = 2,
  }

  public class ModuleDbVersionInfo {
    public string Schema; 
    public string ModuleName;
    public Version Version = DbVersionInfo.ZeroVersion;

    public ModuleDbVersionInfo(string schema, string moduleName, Version version) {
      Schema = schema;
      ModuleName = moduleName;
      Version = version ?? DbVersionInfo.ZeroVersion; 

    }

  }

  public class DbVersionInfo {
    public static readonly Version ZeroVersion = new Version("0.0.0.0");

    public DbInstanceType InstanceType;
    public Version Version = ZeroVersion; 
    public IList<ModuleDbVersionInfo> Modules = new List<ModuleDbVersionInfo>();
    public IDictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    //Used when loading from DB
    public DbVersionInfo() { }

    public DbVersionInfo(EntityApp app, DbModelConfig config) {
      Version = app.Version;
      foreach (var m in app.Modules) {
        var schema = config.GetSchema(m.Area);
        Modules.Add(new ModuleDbVersionInfo(schema, m.Name, m.Version));
      }
    }

    public ModuleDbVersionInfo GetModule(string schema, string moduleName) {
      return Modules.FirstOrDefault(m => m.Schema == schema && m.ModuleName == moduleName);
    }

    public bool VersionChanged(DbVersionInfo old) {
      if (old == null)
        return true; 
      if (Version != old.Version)
        return true;
      foreach (var mi in this.Modules) {
        var oldM = old.GetModule(mi.Schema, mi.ModuleName);
        if (oldM != null && oldM.Version != mi.Version)
          return true; 
      }
      return false;
    }

  }


}
