using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Web;
using Vita.Modules.WebClient.Json;

namespace Vita.Modules.WebClient {

  /// <summary>Defines WebApiClient options.</summary>
  [Flags]
  public enum ClientOptions {
    /// <summary>Empty flag set.</summary>
    None = 0,
    /// <summary>Accept self-issued certificates from the server, typical in development/testing environments.</summary>
    AllowSelfIssuedCertificate = 1,
    /// <summary>Enable cookes. </summary>
    EnableCookies = 1 << 1,
    /// <summary>Automatically process redirect response from the server.</summary>
    AllowAutoRedirect = 1 << 2,
    /// <summary>Default options: AllowSelfIssuedCertificate, EnableCookies</summary>
    Default = AllowSelfIssuedCertificate | EnableCookies,

    [Obsolete("Use nameMapping (ApiNameMapping) parameter to WebClient constructor.")]
    CamelCaseNames = None,
  }

  public class WebApiClientSettings {
    public string ServiceUrl;
    public ClientOptions Options;
    public ApiNameMapping NameMapping; 
    public IContentSerializer ContentSerializer;
    public IClientErrorHandler ErrorHandler;
    public Action<HttpResponseMessage> ResponseSpy;

    public WebApiClientSettings(string serviceUrl, ClientOptions options = ClientOptions.Default, ApiNameMapping nameMapping = ApiNameMapping.Default, 
          IContentSerializer serializer = null, IClientErrorHandler errorHandler = null, Type badRequestContentType = null, Type serverErrorContentType = null) {
      Util.Check(!string.IsNullOrWhiteSpace(serviceUrl), "ServiceUrl may not be empty.");
      if (serviceUrl.EndsWith("/"))
        serviceUrl = serviceUrl.Substring(0, serviceUrl.Length - 1);
      ServiceUrl = serviceUrl;
      Options = options;
      NameMapping = nameMapping; 
      ContentSerializer = serializer ?? new JsonContentSerializer(options, nameMapping);
      ErrorHandler = errorHandler ?? new DefaultClientErrorHandler(ContentSerializer, badRequestContentType, serverErrorContentType);
    }

  }//class



}
