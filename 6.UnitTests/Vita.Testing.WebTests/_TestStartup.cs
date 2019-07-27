using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore;

using Vita.Entities;
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Tools;
using Vita.Entities.DbInfo;
using Vita.Modules.Login.Mocks;

using Vita.RestClient;
using Vita.Samples.BookStore;
using Vita.Samples.BookStore.SampleData;

namespace Vita.UnitTests.Web {

  public static class TestStartup {
    public static BooksEntityApp BooksApp; 
    public static ApiClient Client;
    public static string LogFilePath;
    internal static IConfigurationRoot AppSettings;
    public static MockLoginMessagingService LoginMessagingService;

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
      // Setup mock messaging service
      LoginMessagingService = new MockLoginMessagingService();
      var loginConfig = BooksApp.GetConfig<Modules.Login.LoginModuleSettings>();
      loginConfig.MessagingService = LoginMessagingService;


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
      Client.Settings.ReceivedError += ApiClient_ReceivedError;
      ApiClient.SharedHttpClientHandler.AllowAutoRedirect = false; //we need it for Redirect test
    }

    private static void ApiClient_ReceivedError(object sender, ApiCallEventArgs e) {
      var callInfo = e.CallInfo; 
      if (callInfo.Exception != null) {
        switch (callInfo.Exception) {
          case null: return;
          case BadRequestException bre:
            callInfo.Exception = new ClientFaultException((IList<ClientFault>)bre.Details);
            break; 
        }
      }
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

    public static SentMessageInfo GetLastMessageTo(string email) {
      System.Threading.Thread.Sleep(50); //sending messages is async, make sure bkgr thread done its job
      return LoginMessagingService.SentMessages.LastOrDefault(m => m.Email == email);
    }


  }//class
}
