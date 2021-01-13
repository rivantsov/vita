﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Data;
using Vita.Data.Driver.InfoSchema;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Services;

namespace Vita.Entities.DbInfo {

  public partial class DbInfoModule : IDbInfoService {
    public DbVersionInfo LoadDbVersionInfo(DbModel dbModel, DbSettings settings, ILog log) {
      var tblDbInfo = dbModel.GetTable(typeof(IDbInfo), throwIfNotFound: false);
      Util.Check(tblDbInfo != null, "DbInfo table not defined in DB Model.");

      // Empty/zero verson
      var versionInfo = new DbVersionInfo();
      // Check that table actually exists in the database
      var driver = settings.Driver; 
      if(!TableExists(settings, tblDbInfo))
        return versionInfo; //zero version
      try {
        var dt = SelectAllFrom(settings, tblDbInfo);
        var appRow = dt.FindRow(nameof(IDbInfo.AppName), dbModel.EntityApp.AppName);
        if(appRow != null) {
          versionInfo.InstanceType = (DbInstanceType)appRow.GetAsInt(nameof(IDbInfo.InstanceType));
          var strVersion = appRow.GetAsString(nameof(IDbInfo.Version));
          Version dbVersion;
          if(Version.TryParse(strVersion, out dbVersion))
            versionInfo.Version = dbVersion;
          var strValues = appRow.GetAsString(nameof(IDbInfo.Values));
          DeserializeValues(versionInfo, strValues);
        } //if appRow
        //Read modules
        var tblDbModuleInfo = dbModel.GetTable(typeof(IDbModuleInfo));
        dt = SelectAllFrom(settings, tblDbModuleInfo);
        foreach(var row in dt.Rows) {
          var moduleName = row.GetAsString(nameof(IDbModuleInfo.ModuleName));
          var schema = row.GetAsString(nameof(IDbModuleInfo.Schema));
          var strModuleVersion = row.GetAsString(nameof(IDbModuleInfo.Version));
          if(!Version.TryParse(strModuleVersion, out Version v))
            v = null;
          versionInfo.Modules.Add(new ModuleDbVersionInfo(schema, moduleName, v));
        }
        return versionInfo;
      } catch(Exception ex) {
        log.LogError("Failed to load DbInfo record:  " + ex.ToLogString());
        //Debugger.Break(); 
        throw;
      }
    }

    public virtual bool TableExists(DbSettings settings, DbTableInfo table) {
      var dialect = settings.Driver.SqlDialect;
      var sql = dialect.GetTableExistsSql(table); 
      var result = settings.Driver.ExecuteRawSelect(settings.SchemaManagementConnectionString, sql);
      return result.Rows.Count > 0;
    }

    public virtual InfoTable SelectAllFrom(DbSettings settings, DbTableInfo table) {
      var dialect = settings.Driver.SqlDialect;
      var sql = dialect.GetTableSelectAllSql(table);
      var result = settings.Driver.ExecuteRawSelect(settings.SchemaManagementConnectionString, sql);
      return result;
    }

    public bool UpdateDbInfo(DbModel dbModel, DbSettings settings, ILog log, Exception exception = null) {
      try {
        var app = dbModel.EntityApp;
        var session = App.OpenSystemSession();
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
          ent.Values = SerializeValues(dbModel.VersionInfo);
          SaveModulesInfo(session, dbModel.VersionInfo);
        } else {
          ent.LastModelUpdateFailed = true;
          ent.LastModelUpdateError = exception.ToLogString();
        }
        session.SaveChanges();
        return true;
      } catch(Exception ex) {
        log.LogError(ex.ToLogString());
        return false;
      }
    }

    private void SaveModulesInfo(IEntitySession session, DbVersionInfo dbVersion) {
      // important - should use EntitySet here; otherwise MySql fails
      var moduleRecs = session.EntitySet<IDbModuleInfo>().ToList();
      foreach(var mi in dbVersion.Modules) {
        var mrec = moduleRecs.FirstOrDefault(r => r.ModuleName == mi.ModuleName && r.Schema == mi.Schema);
        if(mrec == null) {
          mrec = session.NewEntity<IDbModuleInfo>();
          mrec.ModuleName = mi.ModuleName;
          mrec.Schema = mi.Schema;
        }
        mrec.Version = mi.Version.ToString();
      }
    }


    private string SerializeValues(DbVersionInfo versionInfo) {
      if(versionInfo.Values == null || versionInfo.Values.Count == 0)
        return null;
      return string.Join(Environment.NewLine, versionInfo.Values.Select(de => de.Key + "=" + de.Value));
    }

    private void DeserializeValues(DbVersionInfo version, string str) {
      version.Values.Clear();
      if(string.IsNullOrWhiteSpace(str))
        return;
      var lines = str.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
      foreach(var line in lines) {
        var arr = line.Split('=');
        if(arr.Length < 2)
          continue;
        version.Values[arr[0]] = arr[1];
      }
    }


  }//class
} //ns
