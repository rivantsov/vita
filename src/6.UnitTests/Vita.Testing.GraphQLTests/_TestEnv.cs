using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arrest;
using BookStore;
using BookStore.GraphQL;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Logging;
using Newtonsoft.Json;
using NGraphQL.Client;
using NGraphQL.Server;

namespace Vita.Testing.GraphQLTests {

  public static class TestEnv {
    static string LogFilePath = "_graphQLTests.log";
    internal static IConfigurationRoot AppConfiguration;
    public static string ServiceUrl;
    public static string EndPointUrl;
    public static GraphQLClient Client;
    public static RestClient RestClient;
    static IWebHost _webHost;


    public static void Init() {
      if (AppConfiguration != null)
        return;
      if (File.Exists(LogFilePath))
        File.Delete(LogFilePath);
      var builder = new ConfigurationBuilder();
      builder.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appSettings.json");
      AppConfiguration = builder.Build();
      ServiceUrl = AppConfiguration["ServiceUrl"];
      StartService();
      EndPointUrl = ServiceUrl + "/graphql";
      Client = new GraphQLClient(EndPointUrl);
      RestClient = new RestClient(ServiceUrl, badRequestContentType: typeof(List<Vita.Entities.ClientFault>)); 
      // if you want to listen/log request/response
      // Client.RequestCompleted += Client_RequestCompleted;
    }

    public static void StartService() {
      IdentityModelEventSource.ShowPII = true;

      GraphQLAspNetServerStartup.StartGrpaphiql = false; // do not start Graphiql UI, we do not need it in tests
      var hostBuilder = WebHost.CreateDefaultBuilder()
          .ConfigureAppConfiguration((context, config) => { })
          .UseStartup<GraphQLAspNetServerStartup>()
          .UseEnvironment("Development") //To return exception details info on ServerError
          .UseUrls(ServiceUrl)
          ;
      _webHost = hostBuilder.Build();

      _webHost.Start();
      Debug.WriteLine("The service is running on URL: " + ServiceUrl);
    }

    public static void ShutDown() {
      _webHost.StopAsync().Wait();
    }

    public static void FlushLogs() {
      BooksEntityApp.Instance.Flush();
    }

    public static long SqlQueryCountForLastRequest; 

    public static async Task<ServerResponse> PostAsync(string query, IDictionary<string, object> variables = null, 
                     string operationName = null, CancellationToken cancellationToken = default) {
      var prevCounter = GetSqlCounter(); 
      var response = await Client.PostAsync(query, variables, operationName, cancellationToken);
      SqlQueryCountForLastRequest = GetSqlCounter() - prevCounter;
      LogCompletedResponse(response);
      return response; 
    }
    
    public static long GetSqlCounter() {
      return BooksEntityApp.Instance.AppEvents.SelectQueryCounter;
    }

    public static void LogTestMethodStart([CallerMemberName] string testName = null) {
      LogText($@"

==================================== Test Method {testName} ================================================
");
    }

    public static void LogComment(string descr) {
      LogText($@"
// Comment: {descr}
");
    }

    private static void LogCompletedResponse(ServerResponse response) {
      string reqText;
      var req = response.Request;
      if (req.HttpMethod == "GET") {
        reqText = @$"GET, URL: {req.UrlQueryPartForGet} 
                unescaped: {Uri.UnescapeDataString(req.UrlQueryPartForGet)}";
      } else
        reqText = "POST, payload: " + Environment.NewLine + response.Request.Body;
      // for better readability, unescape \r\n
      reqText = reqText.Replace("\\r\\n", Environment.NewLine);
      var jsonResponse = JsonConvert.SerializeObject(response.TopFields, Formatting.Indented);
      var text = $@"
Request: 
{reqText}

Response:
{jsonResponse}

//  time: {response.DurationMs} ms; Select SQL count: {SqlQueryCountForLastRequest}   ------------------------------------------ 

";
      LogText(text);
      if (response.Exception != null)
        LogText(response.Exception.ToString());
    }


    static object _lock = new object();
    public static void LogText(string text) {
      lock (_lock) {
        File.AppendAllText(LogFilePath, text);
      }
    }



  }
}
