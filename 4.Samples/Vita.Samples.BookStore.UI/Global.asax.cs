using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Vita.Web;
using Vita.Entities;
using System.Configuration;
using Vita.Samples.BookStore.SampleData;
using Vita.Samples.BookStore.SampleData.Import;
using System.Collections.Specialized;
using Vita.Data.MsSql;
using Vita.Data;


namespace Vita.Samples.BookStore.UI {
  public class MvcApplication : System.Web.HttpApplication {

    protected void Application_Start() {

      var app = CreateConfigureBooksApp();
      GlobalConfiguration.Configuration.EnsureInitialized();
      //Create sample data, import books
      var session = app.OpenSystemSession();
      if (session.EntitySet<IUser>().Count() == 0)
        SampleDataGenerator.CreateBasicTestData(app);
      if (session.EntitySet<IBook>().Count() < 100) {
        var import = new GoogleBooksImport();
        import.ImportBooks(app, 200); 
      }

      RouteConfig.RegisterRoutes(RouteTable.Routes);
      BundleConfig.RegisterBundles(BundleTable.Bundles);
    }

    public static BooksEntityApp CreateConfigureBooksApp() {
      // set up application
      var protectedSection = (NameValueCollection)ConfigurationManager.GetSection("protected");
      var cryptoKey = protectedSection["LoginInfoCryptoKey"];
      var booksApp = new BooksEntityApp(cryptoKey);
      booksApp.Init();
      var connString = protectedSection["MsSqlConnectionString"];
      var logConnString = protectedSection["MsSqlLogConnectionString"];
      var driver = MsSqlDbDriver.Create(connString);
      var dbOptions = MsSqlDbDriver.DefaultMsSqlDbOptions;
      var logDbSettings = new DbSettings(driver, dbOptions, logConnString);
      booksApp.LoggingApp.ConnectTo(logDbSettings);
      var dbSettings = new DbSettings(driver, dbOptions, connString, upgradeMode: DbUpgradeMode.Always);
      booksApp.ConnectTo(dbSettings);
      //Web Api
      WebHelper.ConfigureWebApi(GlobalConfiguration.Configuration, booksApp, logLevel: Entities.Services.LogLevel.Details);
      return booksApp;
    }


  }//class
}