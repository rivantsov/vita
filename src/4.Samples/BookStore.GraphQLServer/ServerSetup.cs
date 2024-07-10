using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Threading.Tasks;
using Vita.Data.MsSql;
using Vita.Data;
using System.IO;
using BookStore.GraphQL;
using NGraphQL.Server.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.GraphQL {
  public static class ServerSetup {
    public const string LogFilePath = "bin\\_serverLog.log";

    public static Task SetupServer(string serverUrl = null) {

      var builder = WebApplication.CreateBuilder();
      if (serverUrl != null)
        builder.WebHost.UseUrls(serverUrl); //this is for unit tests only
      // add this assembly to let system find the ImageController
      builder.Services.AddControllers().AddApplicationPart(typeof(ServerSetup).Assembly);

      // create and register GraphQLHttpService
      var connStr = builder.Configuration["MsSqlConnectionString"];
      var entApp = CreateBooksEntityApp(connStr); 
      var graphQLServer = new BookStoreGraphQLServer(entApp);
      var x = builder.AddGraphQLServer(graphQLServer);

      var webApp = builder.Build();
      webApp.UseRouting();
      webApp.MapControllers();
      
      webApp.MapGraphQLEndpoint(graphQLServer.Settings);

      var task = Task.Run(() => webApp.Run());
      return task;
    }

    private static BooksEntityApp CreateBooksEntityApp(string connStr) {
      if (BooksEntityApp.Instance != null)
        return BooksEntityApp.Instance;
      var booksApp = new BooksEntityApp();
      if (File.Exists(LogFilePath))
        File.Delete(LogFilePath);
      booksApp.LogPath = LogFilePath;
      booksApp.Init();
      //connect to db
      var driver = new MsSqlDbDriver();
      var dbOptions = driver.GetDefaultOptions();
      var dbSettings = new DbSettings(driver, dbOptions, connStr, upgradeMode: DbUpgradeMode.Always);
      booksApp.ConnectTo(dbSettings);
      //if (RebuildSampleData || DbIsEmpty())
      //  ReCreateSampleData();
      return booksApp;
    }

  }
}
