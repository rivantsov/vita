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

namespace Vita.Modules.WebClient {

  /// <summary>A handy wrapper around HttpClient class. Provides a number of convenient methods to call remote web points. </summary>
  /// <remarks>See unit tests in Vita.UnitTests.WebTests project for example of use. </remarks>
  public class WebApiClient {

    // Note on multi-threading, and reuse of HttpClient: Async methods are thread-safe, see Remarks section here: 
    // https://msdn.microsoft.com/en-us/library/system.net.http.httpclient(v=vs.110).aspx
    // So you can create WebApiClient singleton and reuse it from multiple threads


    /// <summary> The key for remote error (server error) in Data dictionary of the exception thrown. </summary>
    public readonly HttpClient Client;
    public readonly HttpClientHandler InnerHandler;
    public readonly WebApiClientSettings Settings; 

    #region constructors

    public WebApiClient(string baseUrl, ClientOptions options = ClientOptions.Default, 
           ApiNameMapping nameMapping = ApiNameMapping.Default,  Type badRequestContentType = null)
      : this(new WebApiClientSettings(baseUrl, options, nameMapping, badRequestContentType: badRequestContentType)) { }

    public WebApiClient(WebApiClientSettings settings) {
      Util.Check(settings != null, "Settings parameter may not be null.");
      Util.Check(settings.ContentSerializer != null, "Settings.Serializer property may not be null.");
      Settings = settings; 
      InnerHandler = new HttpClientHandler();
      InnerHandler.AllowAutoRedirect = settings.Options.IsSet(ClientOptions.AllowAutoRedirect); 
      // Process options
      if(Settings.Options.IsSet(ClientOptions.EnableCookies)) {
        InnerHandler.UseCookies = true;
        InnerHandler.CookieContainer = new CookieContainer();
      }
      if(Settings.Options.IsSet(ClientOptions.AllowSelfIssuedCertificate))
        ServicePointManager.ServerCertificateValidationCallback = DummyValidateCertificate;
      //Create default HTTP Client for JSon calls
      Client = CreateClient(Settings.ContentSerializer.MediaTypes.ToArray()); 
    }

    private HttpClient CreateClient(params string[] mediaTypes) {
      var client = new HttpClient(InnerHandler); 
      client.MaxResponseContentBufferSize = int.MaxValue;
      //Setup content-type header
      client.DefaultRequestHeaders.Clear();
      if (mediaTypes != null && mediaTypes.Length > 0) {
        var strTypes = string.Join(", ", mediaTypes);
        client.DefaultRequestHeaders.Add("accept", mediaTypes);
      }
      return client; 
    }
    #endregion

    #region Headers, cookies
    public void AddAuthorizationHeader(string headerValue, string scheme = "Basic") {
      Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, headerValue);
    }
    public void AddRequestHeader(string name, string value) {
      var headers = Client.DefaultRequestHeaders;
      if(headers.Contains(name))
        headers.Remove(name);
      headers.Add(name, value);
    }
    public void RemoveRequestHeader(string name) {
      Client.DefaultRequestHeaders.Remove(name);
    }

    public CookieCollection GetCookies() {
      if(InnerHandler.CookieContainer == null)
        return new CookieCollection();
      return InnerHandler.CookieContainer.GetCookies(new Uri(Settings.ServiceUrl));
    }

    #endregion

    #region public get/post/put/delete methods with formatted content

    public async Task<TResult> GetAsync<TResult>(string url, params object[] args) {
      var finalUrl = FormatUrl(url, args);
      var response = await Client.GetAsync(finalUrl);
      var exc = await CheckResponse(response);
      if (exc != null)
        return await Task.FromException<TResult>(exc);
      return await GetResponseContentAsync<TResult>(response);
    }//method

    public async Task<TResult> PostAsync<TContent, TResult>(TContent content, string url, params object[] args) {
      return await PostPutAsync<TResult>(true, content, url, args);
    }

    public async Task<TResult> PutAsync<TContent, TResult>(TContent content, string url, params object[] args) {
      return await PostPutAsync<TResult>(false, content, url, args);
    }

    public async Task<HttpStatusCode> DeleteAsync(string url, params object[] args) {
      url = FormatUrl(url, args);
      var response = await Client.DeleteAsync(url);
      var exc = await CheckResponse(response);
      if (exc != null)
        return await Task.FromException<HttpStatusCode>(exc);
      return response.StatusCode;
    }

    public async Task<byte[]> GetBinaryAsync(string mediaType, string url, params object[] args) {
      var client = CreateClient(mediaType); 
      var finalUrl = FormatUrl(url, args);
      var response = await client.GetAsync(finalUrl);
      var exc = await CheckResponse(response);
      if (exc != null)
        return await Task.FromException<byte[]>(exc);
      var result = await response.Content.ReadAsByteArrayAsync();
      return result;
    }


    public async Task<string> GetStringAsync(string mediaType, string url, params object[] args) {
      var finalUrl = FormatUrl(url, args);
      var client = CreateClient(mediaType); 
      var response = await client.GetAsync(finalUrl);
      var exc = await CheckResponse(response);
      if (exc != null)
        return await Task.FromException<string>(exc);
      var result = await response.Content.ReadAsStringAsync();
      return result;
    }

    public async Task<TResult> CallAsync<TContent, TResult>(HttpMethod method, TContent content, string url, params object[] args) {
      url = FormatUrl(url, args);
      HttpContent httpContent = null;
      if (content != null) 
        httpContent = content is HttpContent ? (content as HttpContent) : Settings.ContentSerializer.Serialize(content);
      var request = new HttpRequestMessage(method, url);
      request.Content = httpContent;
      var response = await Client.SendAsync(request); 
      var exc = await CheckResponse(response);
      if(exc != null)
        return await Task.FromException<TResult>(exc);
      return await GetResponseContentAsync<TResult>(response);
    }//method
    #endregion


    #region private methods
    private async Task<TResult> PostPutAsync<TResult>(bool post, object data, string url, params object[] args) {
      url = FormatUrl(url, args);
      HttpContent content = data is HttpContent ? (HttpContent)data : Settings.ContentSerializer.Serialize(data);
      HttpResponseMessage response;
      if(post)
        response = await Client.PostAsync(url, content);
      else
        response = await Client.PutAsync(url, content);
      var exc = await CheckResponse(response);
      if (exc != null)
        return await Task.FromException<TResult>(exc);
      return await GetResponseContentAsync<TResult>(response); 
    }

    private async Task<TResult> GetResponseContentAsync<TResult>(HttpResponseMessage response) {
      if(typeof(TResult) == typeof(HttpResponseMessage))
        return (TResult)(object)response;
      if(typeof(TResult) == typeof(HttpStatusCode))
        return (TResult)(object)response.StatusCode;
      if(typeof(TResult) == typeof(HttpContent))
        return (TResult)(object)response.Content;
      if(typeof(System.IO.Stream).IsAssignableFrom(typeof(TResult))) {
        var stream = await response.Content.ReadAsStreamAsync();
        return (TResult)(object)stream;
      }
      var result = await Settings.ContentSerializer.DeserializeAsync(typeof(TResult), response.Content);
      return (TResult)result;
    }



    private async Task<Exception> CheckResponse(HttpResponseMessage response) {
      var spy = Settings.ResponseSpy;
      if (spy != null)
        spy(response); 
      if (response.IsSuccessStatusCode)
        return null;
      var exc = await Settings.ErrorHandler.HandleErrorAsync(response);
      return exc;
    }

    private string FormatUrl(string template, params object[] args) {
      string fullTemplate;
      if(string.IsNullOrWhiteSpace(template))
        return Settings.ServiceUrl; 
      if (template.StartsWith("http://") || template.StartsWith("https://")) //Check if template is abs URL
        fullTemplate = template;
      else {
        var needDelim = !(template.StartsWith("/") || template.StartsWith("?"));
        var delim = needDelim ? "/" : string.Empty;
        fullTemplate = Settings.ServiceUrl + delim + template;
      }
      return fullTemplate.FormatUri(args);
    }

    //For staging sites, to allow using https with self-issued certificates - always returns true
    private static bool DummyValidateCertificate(Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
      return true;
    }
    #endregion

    #region static constructor
    // Register exception as 'NoWrap', to rethrow as-is when passing asyn-> sync boundary. 
    // This simplifies handling the exception for calling code, if for example it wants to catch 
    // BadRequestException separately. Without this registration all exceptions in async method are 
    // rethrown wrapped in AggregateException on the calling 'sync' thread.
    static WebApiClient() {
      AsyncHelper.AddNoWrapExceptions(typeof(ApiException));
    }
    #endregion
  }//class
}
