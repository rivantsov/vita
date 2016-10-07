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

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Authorization;
using Vita.Entities.Caching;
using Vita.Entities.Model;
using Vita.Entities.Services;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;

using Vita.Modules.Logging;
using Vita.Modules.DbInfo;
using Vita.Modules.EncryptedData;
using Vita.Tools;
using Vita.UnitTests.Common;
using Vita.Samples.BookStore;
using Vita.Samples.BookStore.SampleData;
using Vita.Modules.Notifications;

namespace Vita.UnitTests.Extended {

  internal class TestConfig {
    public DbServerType ServerType;
    public bool EnableCache;
    public bool UseStoredProcs;
    public bool UseBatchMode;
    public override string ToString() {
      return StringHelper.SafeFormat("SERVER TYPE: {0}  StoredProcs: {1}, BatchMode: {2},  EntityCache: {3}", ServerType, UseStoredProcs, UseBatchMode, EnableCache);
    }
  }


  public static class Startup {
    public static string ConnectionString;
    public static string LogConnectionString;
    public static string LoginCryptoKey; 
    public static DbDriver Driver; 
    public static DbSettings DbSettings;
    public static string LogFilePath = "_books.log";
    public static string ActivationLogFilePath = "_booksActivation.log"; //schema changes SQLs

    public static DbServerType ServerType;
    public static bool CacheEnabled;
    public static bool UseBatchMode;
    private static bool _initFailed;

    public static NotificationListener NotificationListener;
    public static BooksEntityApp BooksApp;

    public static void InitApp() {
      Util.Check(!_initFailed, "App initialization failed. Cannot run tests. See other tests output for failure details.");
      if(BooksApp != null)
        return;
      try {
        //force randomization of schema update SQLs, to test that they will put in correct order anyway
        DbModelUpdater.Test_RandomizeInitialSchemaUpdatesOrder = true; 
        //Check if Reset was called; if Driver is null, we are running in Test Explorer mode
        if(Driver == null)
          SetupForTestExplorerMode();
        //Setup model, initialize Books module, create database model, update schema -------------------------------------------------
        BooksApp = new BooksEntityApp(LoginCryptoKey);
        BooksApp.LogPath = LogFilePath;
        BooksApp.ActivationLogPath = ActivationLogFilePath;
        BooksApp.CacheSettings.CacheEnabled = CacheEnabled;
        NotificationListener = new NotificationListener(BooksApp, blockAll: true);   //SmtpMock for testing password reset and other processes
        BooksApp.Init();
        //Reset Db and drop schema objects; first set schema list 
        var resetDb = ConfigurationManager.AppSettings["ResetDatabase"] == "true";
        if(resetDb) {
          DbSettings.SetSchemas(BooksApp.Areas.Select(a => a.Name));
          Vita.UnitTests.Common.TestUtil.DropSchemaObjects(DbSettings);
        }
        //Now connect the main app
        BooksApp.ConnectTo(DbSettings);
        //if we have logging app as a separate app - we need to connect it too. 
        // NOTE: good pracice to connect LoggingApp before we connect the main app, so it can log main database update scripts
        // but it should work anyway.
        var logDbSettings = new DbSettings(Driver, DbSettings.ModelConfig.Options, LogConnectionString);
        BooksApp.LoggingApp.ConnectTo(logDbSettings);

        Thread.Yield(); 
        CreateSampleData();

      } catch(ClientFaultException cfx) {
        Debug.WriteLine("Validation errors: \r\n" + cfx.ToString());
        throw;
      } catch(Exception sx) {
        _initFailed = true;
        //Unit test framework shows only ex message, not details; let's write specifics into debug output - it will be shown in test failure report
        Debug.WriteLine("app init encountered errors: ");
        Debug.WriteLine(sx.ToLogString());
        throw;
      }
    }

    //Prepares for full run with a specified server
    internal static void Reset(TestConfig config) {
      if(BooksApp != null)
        BooksApp.Flush(); 
      Thread.Sleep(100); //to allow log dump of buffered messages
      DeleteLogFiles(); //it will happen only once
      WriteLog("\r\n------------------------ " + config.ToString() + "---------------------------------------------\r\n\r\n");

      ServerType = config.ServerType; 
      CacheEnabled = config.EnableCache;
      UseBatchMode = config.UseBatchMode;
      BooksApp = null; 
      _initFailed = false;

      var protectedSection = (NameValueCollection)ConfigurationManager.GetSection("protected");
      //Load connection string
      ConnectionString = ReplaceBinFolderToken(protectedSection[ServerType + "ConnectionString"]);
      Util.Check(!string.IsNullOrEmpty(ConnectionString), "Connection string not found for server: {0}.", ServerType);
      LogConnectionString = ReplaceBinFolderToken(protectedSection[ServerType + "LogConnectionString"]);
      LogConnectionString = LogConnectionString ?? ConnectionString; 

      LoginCryptoKey = protectedSection["LoginInfoCryptoKey"];
      Driver = ToolHelper.CreateDriver(ServerType, ConnectionString);
      var dbOptions = ToolHelper.GetDefaultOptions(ServerType); 

      if (config.UseStoredProcs)
        dbOptions |= DbOptions.UseStoredProcs;
      else
        dbOptions &= ~DbOptions.UseStoredProcs;
      if (config.UseBatchMode)
        dbOptions |= DbOptions.UseBatchMode;
      else
        dbOptions &= ~DbOptions.UseBatchMode;

      // dbOptions |= DbOptions.ForceArraysAsLiterals; -- just to test this flag      
      DbSettings = new DbSettings(Driver, dbOptions, ConnectionString, upgradeMode: DbUpgradeMode.Always);
      //Test: remap login schema into login2
      // if (ServerType == DbServerType.MsSql)
      //    DbSettings.ModelConfig.MapSchema("login", "login2");
    }

    private static string ReplaceBinFolderToken(string value) {
      if(value == null)
        return null; 
      if(value.Contains("{bin}")) {
        var asmPath = Assembly.GetExecutingAssembly().Location;
        var binFolder = Path.GetDirectoryName(asmPath);
        value = value.Replace("{bin}", binFolder);
      }
      return value; 
    }

    private static void SetupForTestExplorerMode() {
      var servTypeStr = ConfigurationManager.AppSettings["ServerTypeForTestExplorer"];
      DbServerType servType;
      if (!TestUtil.TryParseServerType(servTypeStr, out servType))
        servType = DbServerType.MsSql;
      var enableCache = ConfigurationManager.AppSettings["EnableCacheForTestExplorer"] == "true";
      var useBatchMode = ConfigurationManager.AppSettings["UseBatchModeForTestExplorer"] == "true";
      var useStoredProcs = ConfigurationManager.AppSettings["UseStoredProcsForTestExplorer"] == "true";
      Reset(new TestConfig() { ServerType = servType, EnableCache = enableCache, UseBatchMode = useBatchMode, UseStoredProcs = useStoredProcs });
    }

    //Delete log file only once at app startup; important when running in batch mode for multiple servers
    static bool _logFilesDeleted;
    internal static void DeleteLogFiles() {
      if(_logFilesDeleted)
        return;
      if(File.Exists(LogFilePath))
        File.Delete(LogFilePath);
      if(File.Exists(ActivationLogFilePath))
        File.Delete(ActivationLogFilePath);
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
    }

    public static void InvalidateCache(bool waitForReload = false) {
      if (!CacheEnabled)
        return;
      var ds = BooksApp.GetDefaultDataSource();
      if(ds.Cache != null)
        ds.Cache.Invalidate(reload: waitForReload, waitForReloadComplete: waitForReload); 
    }

    private static void CreateSampleData() {
      var cacheStt = BooksApp.CacheSettings;
      var saveCacheEnabled = cacheStt.CacheEnabled;
      cacheStt.CacheEnabled = false; 

      var entitiesToClear = BooksApp.Model.GetAllEntityTypes().ToList();
      // We create sample data multiple times, so later test wipes out data from previous test. 
      // We do not wipe out certain tables
      TestUtil.DeleteAllData(BooksApp, exceptEntities: 
        new Type[] {typeof(IErrorLog), typeof(IDbInfo), typeof(IDbModuleInfo)}); 
      if (BooksApp.LoggingApp != null)
        TestUtil.DeleteAllData(BooksApp.LoggingApp, exceptEntities:
          new Type[] { typeof(IErrorLog), typeof(IDbInfo), typeof(IDbModuleInfo), typeof(IDbUpgradeBatch), typeof(IDbUpgradeScript) }); 

      SampleDataGenerator.CreateUnitTestData(BooksApp);
      cacheStt.CacheEnabled = saveCacheEnabled; 
    }

    public static NotificationMessage GetLastMessageTo(string email) {
      Thread.Sleep(50); //sending messages is async, make sure bkgr thread done its job
      return NotificationListener.GetLastMessageTo(email);
    }

  }//class
}
