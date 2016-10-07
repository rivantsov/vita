using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Security;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

using Vita.Common;
using Vita.Entities;
using Vita.Modules.WebClient.Json;
using Vita.Entities.Web;
using Vita.Modules.Logging;

namespace Vita.Modules.WebClient {

  /// <summary>A handy wrapper around HttpClient class. Provides a number of convenient methods to call remote web points. </summary>
  /// <remarks>See unit tests in Vita.UnitTests.WebTests project for example of use. </remarks>
  public class WebApiClient {

    // Note on multi-threading, and reuse of HttpClient: Async methods are thread-safe, see Remarks section here: 
    // https://msdn.microsoft.com/en-us/library/system.net.http.httpclient(v=vs.110).aspx
    // Turns out you MUST use a global singleton of HttpClient: 
    //   http://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/
    //   https://www.infoq.com/news/2016/09/HttpClient
    // So we create WebApiClient singleton and reuse it globally. 
    //  You can use a separate, specially created instance of HttpClient if you use SetHttpClient method. 
    public static readonly HttpClient SharedHttpClient;
    public static readonly HttpClientHandler SharedHttpClientHandler;

    //For staging sites, to allow using https with self-issued certificates - always returns true
    public void AllowSelfIssuedCertificates() {
      ServicePointManager.ServerCertificateValidationCallback = DummyValidateCertificate;
    }

    public readonly WebApiClientSettings Settings;
    public readonly Dictionary<string, string> DefaultRequestHeaders;
    public readonly OperationContext Context;
    IWebClientLogService _log;


    /// <summary>Internal instance of HttpClient. By default the object is copied from SharedHttpClient.</summary>
    public HttpClient Client { get; private set; }

    #region constructors

    //Static constructor
    static WebApiClient() {
      SharedHttpClientHandler = new HttpClientHandler();
      SharedHttpClient = new HttpClient(SharedHttpClientHandler);
    }

    public WebApiClient(OperationContext context, string baseUrl, ClientOptions options = ClientOptions.Default, 
           ApiNameMapping nameMapping = ApiNameMapping.Default, string clientName = null, 
           Type badRequestContentType = null)
      : this(context, new WebApiClientSettings(baseUrl, options, nameMapping, clientName, badRequestContentType: badRequestContentType)) { }

    public WebApiClient(OperationContext context, WebApiClientSettings settings) {
      Context = context;
      Util.Check(Context != null, "Context parameter may not be null.");
      Util.Check(settings != null, "Settings parameter may not be null.");
      Util.Check(settings.ContentSerializer != null, "Settings.Serializer property may not be null.");
      Settings = settings;
      if (settings.Options.IsSet(ClientOptions.EnableLog))
        _log = Settings.Log ?? Context.App.GetService<IWebClientLogService>();
      //Create default headers with content type for deserializers
      DefaultRequestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      var mediaTypes = Settings.ContentSerializer.MediaTypes;
      if(mediaTypes.Count > 0) 
        DefaultRequestHeaders.Add("accept", string.Join(", ", mediaTypes));
      Client = SharedHttpClient;
    }

    /// <summary>Replaces inner HttpClient with a given instance. By default the HttpClient instance is a global singleton and is shared by all Web API clients. </summary>
    /// <param name="client">HttpClient instance.</param>
    public void SetHttpClient(HttpClient client) {
      Client = client; 
    }
    #endregion

    #region Headers

    public void AddAuthorizationHeader(string headerValue, string scheme = "Bearer") {
      AddRequestHeader("Authorization", scheme + " " + headerValue);
    }
    public void AddRequestHeader(string name, string value) {
      DefaultRequestHeaders[name] = value;
    }
    public void RemoveRequestHeader(string name) {
      if(DefaultRequestHeaders.ContainsKey(name))
        DefaultRequestHeaders.Remove(name);
    }

    #endregion

    #region public get/post/put/delete methods with formatted content

    public async Task<TResult> GetAsync<TResult>(string url, params object[] args) {
      return await SendAsyncInternal<object, TResult>(HttpMethod.Get, null, null, url, args);
    }//method

    public async Task<TResult> PostAsync<TContent, TResult>(TContent content, string url, params object[] args) {
      return await SendAsyncInternal<TContent, TResult>(HttpMethod.Post, null, content, url, args); 
    }

    public async Task<TResult> PutAsync<TContent, TResult>(TContent content, string url, params object[] args) {
      return await SendAsyncInternal<TContent, TResult>(HttpMethod.Put, null, content, url, args);
    }

    public async Task<HttpStatusCode> DeleteAsync(string url, params object[] args) {
      return await SendAsyncInternal<object, HttpStatusCode>(HttpMethod.Delete, null, null, url, args);
    }

    public async Task<byte[]> GetBinaryAsync(string mediaType, string url, params object[] args) {
      var resultContent = await SendAsyncInternal<object, HttpContent>(HttpMethod.Get, mediaType, null, url, args);
      var result = await resultContent.ReadAsByteArrayAsync();
      return result;
    }

    public async Task<string> GetStringAsync(string mediaType, string url, params object[] args) {
      var resultContent = await SendAsyncInternal<object, HttpContent>(HttpMethod.Get, mediaType, null, url, args);
      var result = await resultContent.ReadAsStringAsync();
      return result;
    }

    private IDictionary<string, string> GetFilteredHeaders(string removeKey) {
      return DefaultRequestHeaders.Where(kv => !kv.Key.Equals(removeKey, StringComparison.OrdinalIgnoreCase)).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public async Task<TResult> SendAsync<TContent, TResult>(HttpMethod method, TContent content, string urlTemplate, params object[] args) {
      return await SendAsyncInternal<TContent, TResult>(method, null, content, urlTemplate, args);
    }

    internal async Task<TResult> SendAsyncInternal<TContent, TResult>(HttpMethod method, string explicitMediaType, TContent content, string urlTemplate, params object[] args) {
      const string AcceptHeaderName = "accept";
      SerializedContent serReqContent = null;
      SerializedContent serRespContent = null;
      HttpRequestMessage request = null;
      HttpResponseMessage response = null;
      Exception exception = null;
      var startMs = Context.App.TimeService.ElapsedMilliseconds;
      try {
        var url = FormatUrl(urlTemplate, args);
        request = new HttpRequestMessage(method, url); //create request early
        if(content == null)
          serReqContent = new SerializedContent();
        else {
          var httpCont = content as HttpContent;
          if(httpCont != null)
            //It is already HttpContent; try to read it as string to log it later
            serReqContent = new SerializedContent() { Object = content, Content = httpCont, Raw = await httpCont.SafeReadContent() };
          else
            //it is not HttpContent, so serialize it
            serReqContent = await Settings.ContentSerializer.SerializeAsync(content);
        }
        request.Content = serReqContent.Content;
        //Setup headers; replace accept header if specified explicitly
        foreach(var kv in DefaultRequestHeaders)
          request.Headers.Add(kv.Key, kv.Value);
        if(!string.IsNullOrWhiteSpace(explicitMediaType)) {
          request.Headers.Remove(AcceptHeaderName); //if it does not exist, no problem, it does not throw err
          request.Headers.Add(AcceptHeaderName, explicitMediaType);
        }
        //actually make a call
        response = await Client.SendAsync(request);
        //invoke spy
        Settings.ResponseSpy?.Invoke(response);
        //check error
        if (!response.IsSuccessStatusCode) {
          exception = await Settings.ErrorHandler.HandleErrorAsync(response);
          serRespContent = new SerializedContent() { Content = response.Content, Raw = await response.Content.SafeReadContent() };
        }
        if(exception != null)
          return await Task.FromException<TResult>(exception);
        //Deserialize response
        serRespContent = await GetResponseContentAsync<TResult>(response);
        return (TResult) serRespContent.Object;
      } catch(Exception exc) {
        exception = exc;
        throw; 
      } finally {
        var endMs = Context.App.TimeService.ElapsedMilliseconds;
        if(_log != null)
          _log.Log(this.Context, Settings.ClientName, endMs - startMs, urlTemplate, args, 
              request, response, serReqContent, serRespContent, exception);
      }
    }//method

    #endregion


    #region private methods

    private async Task<SerializedContent> GetResponseContentAsync<TResult>(HttpResponseMessage response) {
      object result = await GetSpecialResultAsync<TResult>(response);
      if (result != null) 
        return new SerializedContent() { Object = result, Content = response.Content, Raw = await response.Content.SafeReadContent() };
      //Deserialize
      var serContent = await Settings.ContentSerializer.DeserializeAsync(typeof(TResult), response.Content);
      return serContent;
    }

    private async Task<object> GetSpecialResultAsync<TResult>(HttpResponseMessage response) {
      object result = null; 
      if(typeof(TResult) == typeof(HttpResponseMessage))
        result = (TResult)(object)response;
      else if(typeof(TResult) == typeof(HttpStatusCode))
        result = (TResult)(object)response.StatusCode;
      else if(typeof(TResult) == typeof(HttpContent))
        result = (TResult)(object)response.Content;
      else if (typeof(System.IO.Stream).IsAssignableFrom(typeof(TResult))) {
        var stream = await response.Content.ReadAsStreamAsync();
        result = (TResult)(object)stream;
      }
      return result; 
    }


    private string FormatUrl(string template, params object[] args) {
      string fullTemplate;
      if(string.IsNullOrWhiteSpace(template))
        fullTemplate = Settings.ServiceUrl;
      else if(template.StartsWith("http://") || template.StartsWith("https://")) //Check if template is abs URL
        fullTemplate = template;
      else {
        var ch0 = template[0];
        var needDelim = ch0 != '/' && ch0 != '?';
        var delim = needDelim ? "/" : string.Empty;
        fullTemplate = Settings.ServiceUrl + delim + template;
      }
      //Add URL authentication parameters
      var authParams = Settings.UrlAuthenticationParams;
      if (!string.IsNullOrWhiteSpace(authParams)) {
        if(fullTemplate.Contains('?'))
          fullTemplate += "&" + authParams; // there are already parameters
        else
          fullTemplate += "?" + authParams; 
      }
      return fullTemplate.FormatUri(args);
    }

    //For staging sites, to allow using https with self-issued certificates - always returns true
    private static bool DummyValidateCertificate(Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
      return true;
    }
    #endregion

  }//class
}
