using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
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

namespace Vita.Testing.GraphQLTests {

  public static class TestEnv {
    static string LogFilePath = "_graphQLTests.log";
    internal static IConfigurationRoot AppConfiguration;
    public static string ServiceUrl;
    public static string EndPointUrl;
    public static GraphQLClient Client;
    public static RestClient RestClient;

    public static void Init() {
      if (AppConfiguration != null)
        return;
      if (File.Exists(LogFilePath))
        File.Delete(LogFilePath);
      var builder = new ConfigurationBuilder();
      builder.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appSettings.json");
      AppConfiguration = builder.Build();
      ServiceUrl = AppConfiguration["ServiceUrl"];
      ServerSetup.SetupServer(ServiceUrl); 
      EndPointUrl = ServiceUrl + "/graphql";
      Client = new GraphQLClient(EndPointUrl);
      RestClient = new RestClient(ServiceUrl); 
      // if you want to listen/log request/gResult
      // Client.RequestCompleted += Client_RequestCompleted;
    }

    public static void FlushLogs() {
      BooksEntityApp.Instance.Flush();
    }

    public static long SqlQueryCountForLastRequest; 

    public static async Task<GraphQLResult> PostAsync(string query, IDictionary<string, object> variables = null, 
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

    private static void LogCompletedResponse(GraphQLResult gResult) {
      string reqText;
      var req = gResult.Request;
      if (req.HttpMethod == "GET") {
        reqText = @$"GET, URL: {req.UrlQueryPartForGet} 
                unescaped: {Uri.UnescapeDataString(req.UrlQueryPartForGet)}";
      } else
        reqText = "POST, payload: " + Environment.NewLine + gResult.Request.Body;
      // for better readability, unescape \r\n
      reqText = reqText.Replace("\\r\\n", Environment.NewLine);
      var jsonResponse = JsonConvert.SerializeObject(gResult.TopFields, Formatting.Indented);
      var text = $@"
Request: 
{reqText}

Response:
{jsonResponse}

//  time: {gResult.DurationMs} ms; Select SQL count: {SqlQueryCountForLastRequest}   ------------------------------------------ 

";
      LogText(text);
      if (gResult.Exception != null)
        LogText(gResult.Exception.ToString());
    }


    static object _lock = new object();
    public static void LogText(string text) {
      lock (_lock) {
        File.AppendAllText(LogFilePath, text);
      }
    }



  }
}
