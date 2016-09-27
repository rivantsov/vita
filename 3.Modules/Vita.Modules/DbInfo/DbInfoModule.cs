using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Data;
using Vita.Data.Driver;

namespace Vita.Modules.DbInfo {


  public class DbInfoModule : EntityModule, IDbInfoService {
    public static readonly Version CurrentVersion = new Version("1.1.0.0");

    public DbInfoModule(EntityArea area) : base(area, "DbInfo", version: CurrentVersion) {
      RegisterEntities(typeof(IDbInfo), typeof(IDbModuleInfo));
      this.RegisterSize("AppVersion", 20);
      App.RegisterService<IDbInfoService>(this); 
    }


    // We cannot use regular access thru EntitySession - db info/version loads at the very start of connecting to db;
    // the entity app is not ready yet (schema not updated, etc).
    public DbVersionInfo LoadDbInfo(DbSettings settings, string appName, Vita.Data.Driver.DbModelLoader loader) {
      var thisSchema = settings.ModelConfig.GetSchema(this.Area); 
      if (!loader.TableExists(thisSchema, "DbInfo"))
        return null;
      var dbInfoTableName = settings.Driver.GetFullName(thisSchema, "DbInfo");
      var dbModuleInfoTableName = settings.Driver.GetFullName(thisSchema, "DbModuleInfo");
      try {
        var dbInfo = new DbVersionInfo();
        var sql = settings.Driver.GetDirectSelectAllSql(thisSchema, "DbInfo"); 
        var dt = loader.ExecuteSelect(sql); 
        var appRow = dt.FindRow("AppName", appName);
        if (appRow != null) {
          dbInfo.InstanceType = (DbInstanceType)appRow.GetAsInt("InstanceType");
          var strVersion = appRow.GetAsString("Version");
          Version dbVersion;
          if (Version.TryParse(strVersion, out dbVersion))
            dbInfo.Version = dbVersion;
          // 'Values' column appears in v 1.1
          if (dt.HasColumn("Values")) {
            var strValues = appRow.GetAsString("Values");
            DeserializeValues(dbInfo, strValues);
          }
        } //if appRow
        //Read modules
        sql = settings.Driver.GetDirectSelectAllSql(thisSchema, "DbModuleInfo");
        dt = loader.ExecuteSelect(sql); 
        foreach(DbRow row in dt.Rows) {
          var moduleName = row.GetAsString("ModuleName");
          var schema = row.GetAsString("Schema");
          var strModuleVersion = row.GetAsString("Version");
          Version v; 
          if (!Version.TryParse(strModuleVersion, out v))
            v = null; 
          dbInfo.Modules.Add(new ModuleDbVersionInfo(schema, moduleName, v));
        }
        return dbInfo;
      } catch (Exception ex) {
        Trace.WriteLine("Failed to load DbInfo record:  " + ex.ToLogString());
        //Debugger.Break(); 
        return null;
      } 
    } //method


    public bool UpdateDbInfo(Database db, Exception exception = null) {
      //Check that db has module's tables; if not, this module is not included in the solution
      var tbl = db.DbModel.GetTable(typeof(IDbInfo));
      if (tbl == null)
        return false; 
      try {
        var app = db.DbModel.EntityApp;
        var session = App.OpenSystemSession();
        // Disable stored procs and disable batch mode
        // session.DisableStoredProcs();
        session.DisableBatchMode();
        // important - should use EntitySet here; otherwise MySql fails
        var ent = session.EntitySet<IDbInfo>().FirstOrDefault(e => e.AppName == app.AppName);
        if(ent == null) {
          ent = session.NewEntity<IDbInfo>();
          ent.Version = app.Version.ToString();
          ent.AppName = app.AppName;
        }
        if(exception == null) {
          ent.Version = app.Version.ToString();
          ent.LastModelUpdateFailed = false;
          ent.LastModelUpdateError = null;
          ent.Values = SerializeValues(db.DbModel.VersionInfo); 
          SaveModulesInfo(session, db.DbModel.VersionInfo);
        } else {
          ent.LastModelUpdateFailed = true;
          ent.LastModelUpdateError = exception.ToLogString();
        }
        // we use db.SaveChanges directly, to make sure we go thru proper database
        var entSession = (Vita.Entities.Runtime.EntitySession)session;
        db.SaveChanges(entSession);
        return true; 
      } catch (Exception ex) {
        App.ActivationLog.Error(ex.ToLogString());
        return false; 
      }
    }

    private string SerializeValues(DbVersionInfo versionInfo) {
      if (versionInfo.Values == null || versionInfo.Values.Count == 0)
        return null;
      return string.Join(Environment.NewLine, versionInfo.Values.Select(de => de.Key + "=" + de.Value));
    }

    private void DeserializeValues(DbVersionInfo version, string str) {
      version.Values.Clear(); 
      if (string.IsNullOrWhiteSpace(str))
        return; 
      var lines = str.Split(new [] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in lines) {
        var arr = line.Split('=');
        if (arr.Length < 2)
          continue;
        version.Values[arr[0]] = arr[1];
      }
    }


    private void SaveModulesInfo(IEntitySession session, DbVersionInfo dbVersion) {
      // important - should use EntitySet here; otherwise MySql fails
      var moduleRecs = session.EntitySet<IDbModuleInfo>().ToList();
      foreach (var mi in dbVersion.Modules) {
        var mrec = moduleRecs.FirstOrDefault(r => r.ModuleName == mi.ModuleName && r.Schema == mi.Schema);
        if (mrec == null) {
          mrec = session.NewEntity<IDbModuleInfo>();
          mrec.ModuleName = mi.ModuleName;
          mrec.Schema = mi.Schema; 
        }
        mrec.Version = mi.Version.ToString(); 
      }
    }

  }//class
} //ns
