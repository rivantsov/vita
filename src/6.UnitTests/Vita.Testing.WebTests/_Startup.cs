using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Arrest;
using Arrest.Xml;
using BookStore;
using BookStore.SampleData;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Entities;
using Vita.Entities.DbInfo;
using Vita.Modules.Logging.Db;
using Vita.Modules.Login.Mocks;
using Vita.Tools;

namespace Vita.Testing.WebTests {

  public static class Startup {
    public static BooksEntityApp BooksApp;
    public static DbLoggingEntityApp LoggingApp; 
    public static RestClient Client;
    public static string LogFilePath;
    internal static IConfigurationRoot AppConfiguration;
    public static MockLoginMessagingService LoginMessagingService;

    public static void Init() {
      try {
        InitImpl(); 
      } catch(Exception ex) {
        Debug.WriteLine(ex.ToLogString());
        throw;
      }
    }

    public static void LoadAppConfig() {
      if (AppConfiguration != null)
        return;
      var builder = new ConfigurationBuilder();
      builder.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appSettings.json");
      AppConfiguration = builder.Build();
    }


    public static void InitImpl() {
      if(BooksApp != null)
        return;
      LoadAppConfig(); 
      LogFilePath = AppConfiguration["LogFilePath"];
      if (File.Exists(LogFilePath))
        File.Delete(LogFilePath);

      BooksApp = new BooksEntityApp();
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
      var connString = AppConfiguration["MsSqlConnectionString"];
      var dbSettings = new DbSettings(driver, dbOptions, connString, upgradeMode: DbUpgradeMode.Always); // schemas);
      BooksApp.ConnectTo(dbSettings);

      // Logging app
      LoggingApp = new DbLoggingEntityApp();
      LoggingApp.ListenTo(BooksApp); 
      var logConnString = AppConfiguration["MsSqlLogConnectionString"];
      var logDbSettings = new DbSettings(driver, dbOptions, logConnString, upgradeMode: DbUpgradeMode.Always); // schemas);
      LoggingApp.ConnectTo(logDbSettings);

      // create sample data
      DataUtility.DeleteAllData(BooksApp, 
        exceptEntities: new Type[] { typeof(IDbInfo), typeof(IDbModuleInfo) });
      SampleDataGenerator.CreateUnitTestData(BooksApp);

      // Start service 
      var serviceUrl = AppConfiguration["ServiceUrl"];
      StartService(serviceUrl);
      // create client
      var useXml = false;
      if (useXml) {
        var clientSettings = new RestClientSettings(serviceUrl, serializer: new XmlContentSerializer(), badRequestContentType: typeof(string)); // typeof(List<ClientFault>));
        Client = new RestClient(clientSettings);
      } else
        Client = new RestClient(serviceUrl, badRequestContentType: typeof(List<ClientFault>)); //json, very simple setup

      RestClient.SharedHttpClientHandler.AllowAutoRedirect = false; //we need it for Redirect test
      // Setup handler converting BadRequestException into ClientFaultException, with list of faults already converted
      Client.Settings.Events.ReceivedError += (s, e) => {
        if (e.CallData.Exception is BadRequestException bre)
          e.CallData.Exception = new ClientFaultException((IList<ClientFault>)bre.Details);
      };
    }

    static IWebHost _webHost;
    public static void StartService(string baseAddress) {
      var hostBuilder = WebHost.CreateDefaultBuilder()
          .ConfigureAppConfiguration((context, config) => { })
          .UseStartup<BookStore.Api.BooksApiStartup>()
          .UseEnvironment("Development") //To return exception details info on ServerError
          .UseUrls(baseAddress)
          ;
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
