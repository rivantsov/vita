using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BookStore.GraphQLServer;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NGraphQL.Client;

namespace Vita.Testing.GraphQLTests {

  public static class TestEnv {
    internal static IConfigurationRoot AppConfiguration;
    public static string ServiceUrl;
    public static GraphQLClient Client; 
    static IWebHost _webHost;


    public static void Init() {
      if (AppConfiguration != null)
        return;
      var builder = new ConfigurationBuilder();
      builder.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appSettings.json");
      AppConfiguration = builder.Build();
      ServiceUrl = AppConfiguration["ServiceUrl"];
      StartService();
      Client = new GraphQLClient(ServiceUrl + "/graphql");
    }

    public static void StartService() {
      GraphQLAspNetServerStartup.StartGrpaphiql = false; // do not start Graphiql UI, we do not need it in tests
      var hostBuilder = WebHost.CreateDefaultBuilder()
          .ConfigureAppConfiguration((context, config) => { })
          .UseStartup<GraphQLAspNetServerStartup>()
          .UseEnvironment("Development") //To return exception details info on ServerError
          .UseUrls(ServiceUrl)
          ;
      _webHost = hostBuilder.Build();

      _webHost.Start(); 
      Thread.Sleep(10000);
      Debug.WriteLine("The service is running on URL: " + ServiceUrl);
    }

    public static void ShutDown() {
      _webHost.StopAsync().Wait();
    }



  }
}
