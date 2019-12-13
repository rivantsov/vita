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

      Driver = DataUtility.CreateDriver(ServerType);

      //Load connection string
      var connStringName = ServerType.ToString() + "ConnectionString";

      // For SQLite we can use either provider
      var useMsSqlite = AppConfig["UseMsSqliteProvider"] == "true";
      if (ServerType == DbServerType.SQLite && useMsSqlite) {
        var sqliteDriver = (Data.SQLite.SQLiteDbDriver)Driver;
        sqliteDriver.ConnectionFactory = (s) => new Microsoft.Data.Sqlite.SqliteConnection(s);
        sqliteDriver.CommandFactory = () => new Microsoft.Data.Sqlite.SqliteCommand();
        connStringName += "_MS";
      }


      var connString = AppConfig[connStringName];
      Util.Check(!string.IsNullOrEmpty(connString), "Connection string not found for key: {0}.", connStringName);
      if(connString.Contains("{bin}")) {
        var asmPath = Assembly.GetExecutingAssembly().Location;
        var binFolder = Path.GetDirectoryName(asmPath);
        connString = connString.Replace("{bin}", binFolder);
      }
      ConnectionString = connString;


      DbOptions = Driver.GetDefaultOptions();
      //enable batch
      var useBatch = AppConfig["useBatchMode"] == "true";
      if (useBatch && Driver.Supports(DbFeatures.BatchedUpdates))
        DbOptions |= DbOptions.UseBatchMode;
      else
        DbOptions &= ~DbOptions.UseBatchMode;
      //check connection
      if(!DataUtility.TestConnection(Driver, ConnectionString, out var error)) {
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
      var fname = "VitaTestSQLite.db";
      if (File.Exists(fname))
        File.Delete(fname); 
    }

    public static void WriteLog(string message) {
      if(!string.IsNullOrEmpty(LogFilePath))
        System.IO.File.AppendAllText(LogFilePath, message);
    }

    public static EntityApp ActivateApp(EntityApp app, bool dropOldSchema = true, bool dropOldTables = false) {
      //If driver is not set, it means we are running from Test Explorer in VS. Use ServerTypeForTestExplorer
      if(Driver == null)
        SetupForTestExplorerMode();
      app.LogPath = LogFilePath;
      app.ActivationLogPath = ActivationLogPath; 
      try {
        //Setup emitter
        app.EntityClassProvider = Vita.Entities.Emit.EntityClassEmitter.CreateEntityClassProvider(); 
        app.Init();

        var upgradeMode = DbUpgradeMode.Always;
        var upgradeOptions = DbUpgradeOptions.Default;
        if(dropOldTables)
          upgradeOptions |= DbUpgradeOptions.DropUnknownObjects; 
        var dbSettings = new DbSettings(Driver, DbOptions, ConnectionString, upgradeMode: upgradeMode, upgradeOptions: upgradeOptions);

        
        // Drop schema/ delete all
        if(dropOldSchema) {
          DropSchemaObjects(app, dbSettings);
        } 
        app.ConnectTo(dbSettings);
        if (!dropOldSchema)
          DeleteAll(app);
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

    public static void DropSchemaObjects(EntityApp app, DbSettings dbSettings) {
      // SQLite tests starts with a copy of an empty database, no need to delete (it fails in fact)
      if(ServerType == DbServerType.SQLite)
        return;
      try {
        DataUtility.DropSchemaObjects(app, dbSettings);
      } catch(Exception ex) {
        var log = ex.ToLogString();
        app.ActivationLog.Error(log);
        Debug.WriteLine("EntityApp init exception: ");
        Debug.WriteLine(log);
        throw; 
      }
    }


    private static void DeleteSqliteDbFile(string fname) {
      if (File.Exists(fname))
        File.Delete(fname);
    }


    public static void DeleteAll(EntityApp app) {
      DataUtility.DeleteAllData(app); 
    }

    public static DbModel LoadDbModel(EntityApp app) {
      var dbSettings = new DbSettings(Driver, DbOptions, ConnectionString);
      var dbModel = DataUtility.LoadDbModel(app, dbSettings);
      var log = app.ActivationLog;
      if(log.HasErrors)
        Util.Throw("Model loading errors: \r\n" + log.GetAllAsText());
      return dbModel; 
    }

  }//class

}
