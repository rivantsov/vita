using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Diagnostics;

using Vita.Entities;
using Vita.Entities.Services;
using Vita.Web;
using Vita.Data;
using Vita.Data.Model;
using Vita.Data.MsSql;

using Vita.Samples.BookStore;
using Vita.Samples.BookStore.SampleData;
using Vita.RestClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore;
using Vita.Tools;
using Vita.Entities.DbInfo;

namespace Vita.UnitTests.Web {

  public static class TestStartup {
    public static BooksEntityApp BooksApp; 
    public static ApiClient Client;
    public static string LogFilePath;
    internal static IConfigurationRoot AppSettings;

    public static void Init() {
      try {
        InitImpl(); 
      } catch(Exception ex) {
        Debug.WriteLine(ex.ToLogString());
        throw;
      }
    }

    public static void InitAppSettings() {
      if (AppSettings != null)
        return;
      var builder = new ConfigurationBuilder();
      builder.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appSettings.json");
      AppSettings = builder.Build();
    }


    public static void InitImpl() {
      if(BooksApp != null)
        return;
      InitAppSettings(); 
      LogFilePath = AppSettings["LogFilePath"];
      if (File.Exists(LogFilePath))
        File.Delete(LogFilePath);

      BooksApp = new BooksEntityApp();
      BooksApp.EntityClassProvider = Vita.Entities.Emit.EntityClassEmitter.CreateEntityClassProvider();
      BooksApp.LogPath = LogFilePath; 
      //Add mock email/sms service
      // NotificationListener = new NotificationListener(BooksApp, blockAll: true);
      //Set magic captcha in login settings, so we can pass captcha in unit tests
      var loginStt = BooksApp.GetConfig<Vita.Modules.Login.LoginModuleSettings>();
      BooksApp.Init();
      //connect to database
      var driver = new MsSqlDbDriver();
      var dbOptions = MsSqlDbDriver.DefaultMsSqlDbOptions;
      var connString = AppSettings["MsSqlConnectionString"];
      var dbSettings = new DbSettings(driver, dbOptions, connString, upgradeMode: DbUpgradeMode.Always); // schemas);
      BooksApp.ConnectTo(dbSettings);
      // create sample data
      DataUtility.DeleteAllData(BooksApp, 
        exceptEntities: new Type[] { typeof(IDbInfo), typeof(IDbModuleInfo) });
      SampleDataGenerator.CreateUnitTestData(BooksApp);

      // Start service 
      var serviceUrl = AppSettings["ServiceUrl"];
      StartService(serviceUrl);
      // create client
      var clientContext = new OperationContext(BooksApp);
      // change options to None to disable logging of test client calls        
      Client = new ApiClient(serviceUrl, clientContext, clientName : "TestClient", nameMapping: ApiNameMapping.Default, 
           badRequestContentType: typeof(List<ClientFault>));
      ApiClient.SharedHttpClientHandler.AllowAutoRedirect = false; //we need it for Redirect test
    }

    private static void CreateSampleData() {
    }

    static IWebHost _webHost; 
    public static void StartService(string baseAddress) {
      var hostBuilder = WebHost.CreateDefaultBuilder()
          .ConfigureAppConfiguration((context, config) => { })
          .UseStartup<Samples.BookStore.Api.ApiStartup>()
          .UseUrls(baseAddress);
      _webHost = hostBuilder.Build();
      
      Task.Run(()=> _webHost.Run());
      Debug.WriteLine("The service is running on URL: " + baseAddress);
    }


    public static void ShutDown() {
      BooksApp?.Flush();
      _webHost.StopAsync().Wait(); 
    }

    /*
    public static NotificationMessage GetLastMessageTo(string email) {
      System.Threading.Thread.Sleep(50); //sending messages is async, make sure bkgr thread done its job
      return NotificationListener.GetLastMessageTo(email);
    }
    */


  }//class
}
