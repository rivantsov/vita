using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Configuration;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.UnitTests.Common;
using Vita.Tools;
using Vita.Entities.Logging;

namespace Vita.UnitTests.Basic {

  public static class Startup {
    public static DbServerType ServerType;
    public static DbDriver Driver;
    public static DbOptions DbOptions; 
    public static string ConnectionString;
    public static string LogFilePath = "_vitaBasicTests.log";

    //Prepares for full run with a specified server
    public static void Reset(DbServerType serverType) {
      ServerType = serverType;
      //Load connection string
      var connStringName = ServerType.ToString() + "ConnectionString";
      var connString = ConfigurationManager.AppSettings[connStringName];
      Util.Check(!string.IsNullOrEmpty(connString), "Connection string not found for key: {0}.", connStringName);
      if(connString.Contains("{bin}")) {
        var asmPath = Assembly.GetExecutingAssembly().Location;
        var binFolder = Path.GetDirectoryName(asmPath);
        connString = connString.Replace("{bin}", binFolder);
      }
      ConnectionString = connString;
      Driver = ToolHelper.CreateDriver(ServerType); //, ConnectionString);
      DbOptions = ToolHelper.GetDefaultOptions(ServerType);
      //enable stored procs
      DbOptions &= ~DbOptions.UseStoredProcs; //it is on by default
      var useSp = ConfigurationManager.AppSettings["useStoredProcs"] == "true";
      if (useSp && Driver.Supports(DbFeatures.StoredProcedures))
        DbOptions |= Data.DbOptions.UseStoredProcs;
      //enable batch
      var useBatch = ConfigurationManager.AppSettings["useBatchMode"] == "true";
      if(useBatch && Driver.Supports(DbFeatures.BatchedUpdates))
        DbOptions |= DbOptions.UseBatchMode;
      //check connection
      string error;
      if(!ToolHelper.TestConnection(Driver, ConnectionString, out error)) {
        Util.Throw("Failed to connection to the database: {0} \r\n  Connection string: {1}", error, ConnectionString);
      }
    }

    internal static void SetupForTestExplorerMode() {
      var servTypeStr = ConfigurationManager.AppSettings["ServerTypeForTestExplorer"];
      DbServerType servType = ReflectionHelper.ParseEnum<DbServerType>(servTypeStr);
      Reset(servType);
    }

    //Make sure we do it once, when we run multiple times in console mode
    static bool _logFileDeleted;
    internal static void DeleteLocalLogFile() {
      if(_logFileDeleted)
        return;
      if(File.Exists(LogFilePath))
        File.Delete(LogFilePath);
      _logFileDeleted = true;
    }

    public static void WriteLog(string message) {
      if(!string.IsNullOrEmpty(LogFilePath))
        System.IO.File.AppendAllText(LogFilePath, message);
    }

    public static EntityApp ActivateApp(EntityApp app, bool updateSchema = true, bool dropUnknownTables = false) {
      DeleteLocalLogFile();
      app.LogPath = LogFilePath;
      //If driver is not set, it means we are running from Test Explorer in VS. Use ServerTypeForTestExplorer
      if(Driver == null)
        SetupForTestExplorerMode();
      try {
        app.Init();
        var upgradeMode = updateSchema ? DbUpgradeMode.Always : DbUpgradeMode.Never;
        var upgradeOptions = DbUpgradeOptions.Default;
        if(dropUnknownTables)
          upgradeOptions |= DbUpgradeOptions.DropUnknownObjects; 
        var dbSettings = new DbSettings(Driver, DbOptions, ConnectionString, upgradeMode: upgradeMode, upgradeOptions: upgradeOptions); 
        app.ConnectTo(dbSettings);
        return app;
      } catch (StartupFailureException sx) {
        //Unit test framework shows only ex message, not details; let's write specifics into debug output - it will be shown in test failure report
        Debug.WriteLine("EntityApp init exception: ");
        Debug.WriteLine(sx.Log);
        throw;
      }
    }

    public static void DeleteAll(EntityApp app, params Type[] entitiesToDelete) {
      TestUtil.DeleteAllData(app, entitiesToDelete); 
    }

    public static void DropSchemaObjects(string schema) {
      if (Driver == null)
        SetupForTestExplorerMode();
      //SQLite and SqlCe do not support schemas so we effectively wipe out database for each test
      var dbSettings = new DbSettings(Driver, DbOptions, ConnectionString);
      dbSettings.SetSchemas(new[] { schema });
      TestUtil.DropSchemaObjects(dbSettings); 
    }      

    public static DbModel LoadDbModel(string schema, MemoryLog log) {
      if (Driver == null)
        SetupForTestExplorerMode();
      var dbSettings = new DbSettings(Driver, DbOptions, ConnectionString);
      dbSettings.SetSchemas(new[] { schema });
      var loader = Driver.CreateDbModelLoader(dbSettings, log);
      var dbModel = loader.LoadModel();
      if (log.HasErrors())
        Util.Throw("Model loading errors: \r\n" + log.GetAllAsText());
      return dbModel;
    }

  }//class

}
