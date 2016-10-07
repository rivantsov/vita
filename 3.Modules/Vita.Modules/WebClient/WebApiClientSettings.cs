using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Web;
using Vita.Modules.Logging;
using Vita.Modules.WebClient.Json;

namespace Vita.Modules.WebClient {

  /// <summary>Defines WebApiClient options.</summary>
  [Flags]
  public enum ClientOptions {
    /// <summary>Empty flag set.</summary>
    None = 0,
    EnableLog = 1,

    Default = EnableLog,

    [Obsolete("Use nameMapping (ApiNameMapping) parameter in WebClientSettings constructor instead.")]
    CamelCaseNames = None,
  }

  public class WebApiClientSettings {
    public string ServiceUrl;
    public ClientOptions Options;
    public ApiNameMapping NameMapping;
    public string UrlAuthenticationParams;
    public IContentSerializer ContentSerializer;
    public IClientErrorHandler ErrorHandler;
    public Action<HttpResponseMessage> ResponseSpy;
    /// <summary>Log service. If this property is set, the client uses this instance 
    /// instead of retrieving log service from the app.</summary>
    public IWebClientLogService Log;
    /// <summary>Identifier of the client, written to log, to easier identify entries.</summary>
    public string ClientName;

    public WebApiClientSettings(string serviceUrl, ClientOptions options = ClientOptions.Default, 
          ApiNameMapping nameMapping = ApiNameMapping.Default, string clientName = null,
          IContentSerializer serializer = null, IClientErrorHandler errorHandler = null, IWebClientLogService log = null,
          Type badRequestContentType = null, Type serverErrorContentType = null) {
      Util.Check(!string.IsNullOrWhiteSpace(serviceUrl), "ServiceUrl may not be empty.");
      if (serviceUrl.EndsWith("/"))
        serviceUrl = serviceUrl.Substring(0, serviceUrl.Length - 1);
      ServiceUrl = serviceUrl;
      Options = options;
      NameMapping = nameMapping;
      ClientName = clientName;
      ContentSerializer = serializer ?? new JsonContentSerializer(options, nameMapping);
      ErrorHandler = errorHandler ?? new DefaultClientErrorHandler(ContentSerializer, badRequestContentType, serverErrorContentType);
      Log = log; 
    }

  }//class



}
