using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Configuration;
using System.Threading;
using System.Reflection;
using System.IO;
using System.Collections.Specialized;

using Vita.Entities;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;

using BookStore;
using BookStore.SampleData;
using Microsoft.Extensions.Configuration;
using Vita.Tools;
using Vita.Entities.DbInfo;
using Vita.Tools.Testing;
using Vita.Data.Sql;
using Vita.Modules.Login.Mocks;
using System.Runtime.CompilerServices;
using Vita.Entities.Logging;
//using Microsoft.Data.Sqlite;

namespace Vita.Testing.ExtendedTests {


  public static class Startup {
    public static TestRunConfig CurrentConfig; 
    public static DbDriver Driver; 
    public static DbSettings DbSettings;
    public static string LogFilePath = "_books.log";
    public static string ErrorLogFilePath = "_errors.log"; //schema changes SQLs

    public static DbServerType ServerType;
    //public static bool CacheEnabled;
    public static bool UseBatchMode;
    private static bool _initFailed;

    public static MockLoginMessagingService LoginMessagingService;
    public static BooksEntityApp BooksApp;

    internal static IConfigurationRoot AppSettings;

    public static void InitAppSettings() {
      if(AppSettings != null)
        return;
      var builder = new ConfigurationBuilder();
      builder.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appSettings.json");      
      AppSettings = builder.Build();
    }

    public static void InitApp() {
      Util.Check(!_initFailed, "App initialization failed. Cannot run tests. See other tests output for failure details.");
      if(BooksApp != null)
        return;
      try {
        //force randomization of schema update SQLs, to test that they will be put in correct order anyway
        DbModelUpdater.Test_RandomizeInitialSchemaUpdatesOrder = true; 
        //Check if Reset was called; if Driver is null, we are running in Test Explorer mode
        if(Driver == null)
          SetupForTestExplorerMode();
        if (ServerType == DbServerType.SQLite)
          DeleteSqliteDbFile("VitaBooksSQLite");
        //Setup model, initialize Books module, create database model, update schema -------------------------------------------------
        BooksApp = new BooksEntityApp();
        BooksApp.LogPath = LogFilePath;
        BooksApp.ErrorLogPath = ErrorLogFilePath;
        BooksApp.ActivationLogPath = "_activation.log";
        BooksApp.Init();
        //File.WriteAllText("_activation.Log",  BooksApp.ActivationLog.GetAllAsText()); 

        // Oracle - uncomment this to see tablespace use, but you must pre-create the tablespace in SQL-Developer
        /* 
        if(ServerType == DbServerType.Oracle)
          foreach(var area in BooksApp.Areas)
             area.OracleTableSpace = "Books";
        */

        // Setup mock messaging service
        LoginMessagingService = new MockLoginMessagingService(); 
        var loginConfig = BooksApp.GetConfig<Modules.Login.LoginModuleSettings>();
        loginConfig.MessagingService = LoginMessagingService;

        //Reset Db and drop schema objects; first set schema list 
        var resetDb = AppSettings["ResetDatabase"] == "true";
        if(resetDb) {
          DataUtility.DropSchemaObjects(BooksApp, DbSettings);
        }

        //Now connect the main app
        BooksApp.ConnectTo(DbSettings);

        CreateSampleData();
        // delete sample data log
        BooksApp.Flush();

        if (File.Exists(LogFilePath))
          File.Delete(LogFilePath);

      } catch (ClientFaultException cfx) {
        Debug.WriteLine("Validation errors: \r\n" + cfx.ToString());
        _initFailed = true;
        throw;
      } catch(Exception sx) {
        _initFailed = true;
        //Unit test framework shows only ex message, not details; let's write specifics into debug output - it will be shown in test failure report
        Debug.WriteLine("app init encountered errors: ");
        Debug.WriteLine(sx.ToLogString());

        throw;
      }
    }


    private static void DeleteSqliteDbFile(string fname) {
      if (File.Exists(fname))
        File.Delete(fname);
    }

    //Prepares for full run with a specified server
    internal static void Reset(TestRunConfig config) {
      CurrentConfig = config; 
      ServerType = config.ServerType;
      if(BooksApp != null)
        BooksApp.Flush(); 
      Thread.Sleep(100); //to allow log dump of buffered messages
      DeleteLogFiles(); //it will happen only once
      WriteLog("\r\n------------------------ " + config.ToString() + "---------------------------------------------\r\n\r\n");

      ServerType = config.ServerType; 
      UseBatchMode = config.UseBatchMode;
      BooksApp = null; 
      _initFailed = false;

      //Check connection string
      Util.Check(!string.IsNullOrEmpty(config.ConnectionString), "Connection string not found for server: {0}.", ServerType);

      Driver = DataUtility.CreateDriver(ServerType);
      var dbOptions = Driver.GetDefaultOptions(); 

      if (config.UseBatchMode)
        dbOptions |= DbOptions.UseBatchMode;
      else
        dbOptions &= ~DbOptions.UseBatchMode;

      // Custom naming policy. Uncomment below to see how all-lower case policy works for Postgres
      IDbNamingPolicy customNamingPolicy = null;
      //if(ServerType == DbServerType.Postgres)
        //customNamingPolicy = new AllLowCaseNamingPolicy("books", "login"); 
      DbSettings = new DbSettings(Driver, dbOptions, config.ConnectionString, upgradeMode: DbUpgradeMode.Always, 
                namingPolicy: customNamingPolicy);

      //Test: remap login schema into login2
      // Remapping schemas might used in MySql, where schemas are actually databases
      // if (ServerType == DbServerType.MsSql)
      //    DbSettings.ModelConfig.MapSchema("login", "login2");
    }


    private static void SetupForTestExplorerMode() {
      InitAppSettings();
      var config = TestRunConfigLoader.LoadForTestExplorer(AppSettings);
      Reset(config);
      SqlCacheLogHelper.SetupSqlCacheLog();

    }

    //Delete log file only once at app startup; important when running in batch mode for multiple servers
    static bool _logFilesDeleted;
    internal static void DeleteLogFiles() {
      if(_logFilesDeleted)
        return;
      if(File.Exists(LogFilePath))
        File.Delete(LogFilePath);
      if(File.Exists(ErrorLogFilePath))
        File.Delete(ErrorLogFilePath);
      _logFilesDeleted = true;
    }



    public static void WriteLog(string message) {
      try {
        if(!string.IsNullOrEmpty(LogFilePath))
          System.IO.File.AppendAllText(LogFilePath, message);
      } catch { } //protect against file locked by bkgr writing 
    }


    public static void TearDown() {
      //You should do this normally - shutdown the entity store
      // but in this test app it would take too long time for all tests (re-init database for each test class)
      // so by default running without it
#if FULL_SHUTDOWN
      if (BooksApp != null)
        BooksApp.Shutdown();
      BooksApp = null; 
#endif
      if (BooksApp != null)
        BooksApp.Flush();
      SqlCacheLogHelper.FlushSqlCacheLog(); 
      Thread.Sleep(20);
    }

    private static void CreateSampleData() {
      DataUtility.DeleteAllData(BooksApp, exceptEntities: new Type[] {typeof(IDbInfo), typeof(IDbModuleInfo)}); 
      SampleDataGenerator.CreateUnitTestData(BooksApp);
    }

  }//class
}
