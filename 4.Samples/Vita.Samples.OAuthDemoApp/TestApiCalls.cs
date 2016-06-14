using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Vita.Entities;
using Vita.Modules.OAuthClient;
using Vita.Modules.WebClient;
using Vita.Modules.WebClient.Sync; 

namespace Vita.Samples.OAuthDemoApp {

  public class TestCallInfo {
    public string RequestUrl;
    public string ResponseData; 
  }

  public static class TestApiCalls {

    public static TestCallInfo MakeTestApiCall(IOAuthClientService service, IOAuthAccessToken token) {
      string testUrl = token.Account.Server.BasicProfileUrl; 
      var testUri = new Uri(testUrl); 
      var webClient = new WebApiClient(testUri.Scheme + "://" + testUri.Authority, 
        ClientOptions.Default, typeof(string));
      service.SetupWebClient(webClient, token);
      var respStream = webClient.ExecuteGet<System.IO.Stream>(testUri.AbsolutePath + testUri.Query);
      var reader = new StreamReader(respStream);
      var respText = reader.ReadToEnd();
      return new TestCallInfo() { RequestUrl = testUrl, ResponseData = respText };  
    }//method

    #region LinkedIn


    #endregion

  }//class
}
