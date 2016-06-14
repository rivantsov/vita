using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Modules.WebClient.Json;

namespace Vita.Modules.WebClient {

  [Flags]
  public enum ClientOptions {
    None = 0,
    AllowSelfIssuedCertificate = 1,
    EnableCookies = 1 << 1,
    AllowAutoRedirect = 1 << 2,
    CamelCaseNames = 1 << 4,
    Default = AllowSelfIssuedCertificate | EnableCookies,
  }

  public class WebApiClientSettings {
    public string ServiceUrl;
    public ClientOptions Options;
    public IContentSerializer Serializer;
    public IClientErrorHandler ErrorHandler;
    public Action<HttpResponseMessage> ResponseSpy;

    public WebApiClientSettings(string serviceUrl, ClientOptions options = ClientOptions.Default, 
          IContentSerializer serializer = null, IClientErrorHandler errorHandler = null, Type badRequestContentType = null, Type serverErrorContentType = null) {
      Util.Check(!string.IsNullOrWhiteSpace(serviceUrl), "ServiceUrl may not be empty.");
      if (serviceUrl.EndsWith("/"))
        serviceUrl = serviceUrl.Substring(0, serviceUrl.Length - 1);
      ServiceUrl = serviceUrl;
      Options = options;
      Serializer = serializer ?? new JsonContentSerializer(options);
      ErrorHandler = errorHandler ?? new DefaultClientErrorHandler(Serializer, badRequestContentType, serverErrorContentType);
    }

  }//class



}
