using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Configuration;
using System.Diagnostics;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Utilities;

using Microsoft.Extensions.Configuration;
using Vita.Tools;
using Vita.Tools.Testing;

namespace Vita.Testing.BasicTests {

  internal static class Startup {
    public static DbServerType ServerType;
    public static DbDriver Driver;
    public static DbOptions DbOptions; 
    public static string ConnectionString;
    public static string LogFilePath = "_operationLog.log";
    public static string ActivationLogPath = "_activationLog.log";
    public static IConfigurationRoot AppConfig;

    public static void InitAppConfig() {
      if(AppConfig != null)
        return;
      var builder = new ConfigurationBuilder();
      builder.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appSettings.json");
      AppConfig = builder.Build(); 
    }

    //Prepares for full run with a specified server
    public static void Reset(DbServerType serverType) {
      DeleteLocalLogFiles();
      InitAppConfig(); 
      ServerType = serverType;
      if (ServerType == DbServerType.SQLite)
        DeleteSqliteDbFile(); //it will be created on connect, creat-option in conn string
      //Load connection string
      var connStringName = ServerType.ToString() + "ConnectionString";
      var connString = AppConfig[connStringName];
      Util.Check(!string.IsNullOrEmpty(connString), "Connection string not found for key: {0}.", connStringName);
      if(connString.Contains("{bin}")) {
        var asmPath = Assembly.GetExecutingAssembly().Location;
        var binFolder = Path.GetDirectoryName(asmPath);
        connString = connString.Replace("{bin}", binFolder);
      }
      ConnectionString = connString;
      Driver = ToolHelper.CreateDriver(ServerType); 
      DbOptions = Driver.GetDefaultOptions();
      //enable stored procs
      //enable batch
      var useBatch = AppConfig["useBatchMode"] == "true";
      if (useBatch && Driver.Supports(DbFeatures.BatchedUpdates))
        DbOptions |= DbOptions.UseBatchMode;
      else
        DbOptions &= ~DbOptions.UseBatchMode;
      //check connection
      string error;
      if(!ToolHelper.TestConnection(Driver, ConnectionString, out error)) {
        Util.Throw("Failed to connect to the database: {0} \r\n  Connection string: {1}", error, ConnectionString);
      }
    }

    internal static void SetupForTestExplorerMode() {
      InitAppConfig(); 
      var servTypeStr = AppConfig["ServerTypeForTestExplorer"];
      DbServerType servType = StringHelper.ParseEnum<DbServerType>(servTypeStr);
      Reset(servType);
    }

    //Make sure we do it once, when we run multiple times in console mode
    static bool _logFilesDeleted;
    internal static void DeleteLocalLogFiles() {
      if(_logFilesDeleted)
        return;
      if(File.Exists(LogFilePath))
        File.Delete(LogFilePath);
      if(File.Exists(ActivationLogPath))
        File.Delete(ActivationLogPath);
      _logFilesDeleted = true;
    }

    private static void DeleteSqliteDbFile() {
      var fname = "VitTestSQLite.db";
      if (File.Exists(fname))
        File.Delete(fname); 
    }

    public static void WriteLog(string message) {
      if(!string.IsNullOrEmpty(LogFilePath))
        System.IO.File.AppendAllText(LogFilePath, message);
    }

    public static EntityApp ActivateApp(EntityApp app, bool updateSchema = true, bool dropUnknownTables = false) {
      app.LogPath = LogFilePath;
      app.ActivationLogPath = ActivationLogPath; 
      //If driver is not set, it means we are running from Test Explorer in VS. Use ServerTypeForTestExplorer
      if(Driver == null)
        SetupForTestExplorerMode();
      try {
        //Setup emitter
        app.EntityClassProvider = Vita.Entities.Emit.EntityClassEmitter.CreateEntityClassProvider(); 
        app.Init();

        if (ServerType == DbServerType.SQLite)
          DeleteSqliteDbFile("VitaTestSqlite.db");

        var upgradeMode = updateSchema ? DbUpgradeMode.Always : DbUpgradeMode.Never;
        var upgradeOptions = DbUpgradeOptions.Default;
        if(dropUnknownTables)
          upgradeOptions |= DbUpgradeOptions.DropUnknownObjects; 
        var dbSettings = new DbSettings(Driver, DbOptions, ConnectionString, upgradeMode: upgradeMode, upgradeOptions: upgradeOptions); 
        app.ConnectTo(dbSettings);
        return app;
      } catch (StartupFailureException sx) {
        //Unit test framework shows only ex message, not details; let's write specifics into debug output - it will be shown in test failure report
        app.ActivationLog.Error(sx.Message);
        app.ActivationLog.Info(sx.Log);
        Debug.WriteLine("EntityApp init exception: ");
        Debug.WriteLine(sx.Log);
        throw;
      }
    }

    private static void DeleteSqliteDbFile(string fname) {
      if (File.Exists(fname))
        File.Delete(fname);
    }


    public static void DeleteAll(EntityApp app, params Type[] entitiesToDelete) {
      TestUtil.DeleteAllData(app, null, entitiesToDelete); 
    }

    public static void DropSchemaObjects(string schema) {
      if (Driver == null)
        SetupForTestExplorerMode();
      // SQLite starts with a copy of an empty database, no need to deleate (it fails in fact)
      switch(ServerType) {
        case DbServerType.SQLite: 
          return; 
      }
      //SQLite and SqlCe do not support schemas so we effectively wipe out database for each test
      var dbSettings = new DbSettings(Driver, DbOptions, ConnectionString);
      dbSettings.SetSchemas(new[] { schema });
      TestUtil.DropSchemaObjects(dbSettings); 
    }      

    public static DbModel LoadDbModel(string schema, IActivationLog log) {
      if (Driver == null)
        SetupForTestExplorerMode();
      var dbSettings = new DbSettings(Driver, DbOptions, ConnectionString);
      dbSettings.SetSchemas(new[] { schema });
      var loader = Driver.CreateDbModelLoader(dbSettings, log);
      var dbModel = loader.LoadModel();
      if (log.HasErrors)
        Util.Throw("Model loading errors: \r\n" + log.GetAllAsText());
      return dbModel;
    }

  }//class

}
