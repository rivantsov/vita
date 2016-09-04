using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using System.Web.Security;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Entities.Web;
using Vita.Web.SlimApi;


namespace Vita.Web {

  public static class WebHelper {

    public static void ConfigureWebApi(HttpConfiguration httpConfiguration, EntityApp app, 
                             LogLevel logLevel = LogLevel.Basic,
                             WebHandlerOptions webHandlerOptions = WebHandlerOptions.DefaultDebug, 
                             ApiNameMapping nameMapping = ApiNameMapping.Default) {
      // Logging message handler
      var webHandlerStt = new WebCallContextHandlerSettings(logLevel, webHandlerOptions);
      var webContextHandler = new WebCallContextHandler(app, webHandlerStt);
      httpConfiguration.MessageHandlers.Add(webContextHandler);

      // Exception handling filter - to handle/save exceptions
      httpConfiguration.Filters.Add(new ExceptionHandlingFilter());

      // Formatters - add formatters with spies, to catch/log deserialization failures
      httpConfiguration.Formatters.Clear();
      httpConfiguration.Formatters.Add(new StreamMediaTypeFormatter("image/jpeg", "image/webp")); //webp is for Chrome
      var xmlFmter = new XmlMediaTypeFormatter();
      httpConfiguration.Formatters.Add(xmlFmter);
      var jsonFmter = new JsonMediaTypeFormatter();
      // add converter that will serialize all enums as strings, not integers
      jsonFmter.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
      jsonFmter.SerializerSettings.ContractResolver = new JsonNameContractResolver(nameMapping);
      jsonFmter.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Unspecified;
      httpConfiguration.Formatters.Add(jsonFmter);

      //Api configuration
      if (app.ApiConfiguration.ControllerInfos.Count > 0)
        ConfigureSlimApiControllers(httpConfiguration, app);
    }

    public static void ConfigureSlimApiControllers(HttpConfiguration config, EntityApp app) {
      var actionSelector = new SlimApiActionSelector(app.ApiConfiguration);
      config.Services.Replace(typeof(System.Web.Http.Controllers.IHttpActionSelector), actionSelector);
      var prov = new SlimApiDirectRouteProvider();
      config.MapHttpAttributeRoutes(prov);
     // config.ParameterBindingRules.Add(typeof(DateTime), p => new DateTimeParameterBinding(p));
    }

    /* experiments
    public class DateTimeParameterBinding : HttpRequestParameterBinding {
      public DateTimeParameterBinding(HttpParameterDescriptor d) : base(d) { }
      public override Task ExecuteBindingAsync(System.Web.Http.Metadata.ModelMetadataProvider metadataProvider, HttpActionContext actionContext, System.Threading.CancellationToken cancellationToken) {
        return base.ExecuteBindingAsync(metadataProvider, actionContext, cancellationToken); 
      }
    }
     */ 

    //Helper method - retrieves WebCallInfo object from request properties; optionally creates and saves it if it does not exist. 
    public static WebCallContext GetWebCallContext(this HttpRequestMessage request) {
      if(request == null)
        return null; 
      object propValue;
      if(request.Properties.TryGetValue(WebCallContext.WebCallContextKey, out propValue))
        return (WebCallContext)propValue;
      return null;
    }

    public static WebCallContext GetWebCallContext(ApiController controller) {
      if(controller == null)
        return null;
      var request = controller.ControllerContext.Request;
      var ctx = request.GetWebCallContext();
      if(ctx != null)
        ctx.SetControllerInfo(controller);
      return ctx; 
    }

    /// <summary>Sets controller info in WebCallInfo object. Call it from ApiController.Init method after calling base Init method. </summary>
    public static void SetControllerInfo(this WebCallContext webCallContext, ApiController controller) {
      if(webCallContext == null) // should never happen if you use WebMessageHandler
        return;
      webCallContext.ControllerName = controller.GetType().Name;
      object action;
      // Note: this works for regular routing, not for attribute routing
      controller.ControllerContext.RouteData.Values.TryGetValue("action", out action);
      webCallContext.MethodName = string.Empty + action; //safe ToString() method
    }

    public static long? GetLength(this HttpContent content) {
      if(content == null)
        return null;
      return content.Headers.ContentLength;
    }

    public static string GetHeaderValue(this HttpResponseMessage message, string header) {
      var values = GetHeaderValues(message, header);
      if (values == null) return null;
      if (values.Count == 1) return values[0];
      return string.Join(",", values); 
    }

    public static IList<string> GetHeaderValues(this HttpResponseMessage message, string header) {
      IEnumerable<string> values;
      if(!message.Headers.TryGetValues(header, out values)) return null;
      return values.ToList();
    }

    public static string GetHeaderValue(this HttpRequestMessage message, string header) {
      var values = GetHeaderValues(message, header);
      if (values == null) return null;
      if (values.Count == 1) return values[0];
      return string.Join(",", values);
    }
    public static IList<string> GetHeaderValues(this HttpRequestMessage message, string header) {
      IEnumerable<string> values;
      if(!message.Headers.TryGetValues(header, out values)) return null;
      return values.ToList();
    }

    public static IList<Cookie> GetCookies(this HttpRequestMessage request, string cookieName) {
      var chvs = request.Headers.GetCookies(cookieName); //cookiHeaderValue collection
      if(chvs == null || chvs.Count == 0)
        return null;  
      return chvs.SelectMany(chv => chv.Cookies.Select(cs => new Cookie(cs.Name, cs.GetCookieStateValue(), chv.Path, chv.Domain))).ToList(); 
    }

    private static string GetCookieStateValue(this CookieState cs) {
      if (cs.Value != null)
        return cs.Value; 
      if (cs.Values != null && cs.Values.Count > 0) {
        var pairs = new List<string>(); 
        foreach (string key in cs.Values.Keys) 
          pairs.Add(key + "=" + cs.Values[key]);
        return string.Join(";", pairs);
      } else
        return cs.Value;


    }

    public static void SetCookies(this HttpResponseMessage response, IList<System.Net.Cookie> cookies, string host) {
      if(cookies == null || cookies.Count == 0)
        return; 
      foreach(var ck in cookies) {
        var ch = new CookieHeaderValue(ck.Name, ck.Value);
        ch.Domain = string.IsNullOrWhiteSpace(ck.Domain) ? host : ck.Domain;
        ch.Path = string.IsNullOrWhiteSpace(ck.Path) ? "/" : ck.Path;
        ch.Expires = ck.Expires;
        ch.HttpOnly = ck.HttpOnly;
        //ch.Secure = ck.Secure; 
        if(ck.Discard)
          ch.Expires = new DateTime(2000, 1, 1).ToUniversalTime();
        response.Headers.AddCookies(new [] { ch });
      }
    }
    public static void SetHeaders(this HttpResponseMessage response, WebCallContext webContext) {
      foreach(var kv in webContext.OutgoingHeaders) {
        if (kv.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)) {
          if(response.Content == null)
            continue; 
          var headers = response.Content.Headers;
          if (headers.Contains(kv.Key)) headers.Remove(kv.Key);
          headers.Add(kv.Key, kv.Value); 
        } else {
          var headers = response.Headers; 
          if (headers.Contains(kv.Key)) headers.Remove(kv.Key); 
          headers.Add(kv.Key, kv.Value);
        }
      }
    }

    public static MediaTypeFormatter GetResponseFormatter(this HttpRequestMessage request, Type typeToWrite = null) {
      typeToWrite = typeToWrite ?? typeof(string);
      var cfg = request.GetConfiguration();
      foreach(var mediaTypeHeader in request.Headers.Accept) {
        var writer = cfg.Formatters.FindWriter(typeToWrite, mediaTypeHeader);
        if(writer != null)
          return writer;
      }
      return null; 
    }//method


    // This is available only under IIS web server, not in self-hosting scenario
    internal static HttpContextWrapper GetHttpContextWrapper(HttpRequestMessage request) {
      object propValue;
      if(request.Properties.TryGetValue("MS_HttpContext", out propValue))
        return (HttpContextWrapper)propValue;
      return null;
    }

    //Returns request headers as string, but without some secure headers like  Authorization header
    // (which might contain user password) then web call log is a security leak.
    internal static string GetHeadersAsString(this HttpRequestMessage request, IList<string> hideHeaders) {
      //join headers - omit security related headers to avoid security leaks
      var strings =  request.Headers.Select(h => string.Format("{0}:{1}", 
            h.Key,  hideHeaders.Contains(h.Key) ? "(ValueOmitted)" : string.Join(",", h.Value))).ToList();
      //content headers
      if(request.Content != null && request.Content.Headers != null) 
        strings.AddRange(request.Content.Headers.Select(h => string.Format("{0}:{1}", h.Key,  string.Join(",", h.Value))));
      var result = string.Join(Environment.NewLine, strings);
      return result;
    }
    internal static string GetHeadersAsString(this HttpResponseMessage response, IList<string> hideHeaders) {
      //join headers - omit security related headers to avoid security leaks
      var strings = response.Headers.Select(h => string.Format("{0}:{1}",
            h.Key, hideHeaders.Contains(h.Key) ? "(ValueOmitted)" : string.Join(",", h.Value))).ToList();
      //content headers
      if(response.Content != null && response.Content.Headers != null)
        strings.AddRange(response.Content.Headers.Select(h => string.Format("{0}:{1}", h.Key, string.Join(",", h.Value))));
      var result = string.Join(Environment.NewLine, strings);
      return result;
    }

    /*  This does not work for request under IIS - request.Content needs to be reset (position of the stream)
    var reqRead = content.ReadAsStringAsync();
    reqRead.Wait();
    return reqRead.Result;
     *   so we found a workaround by retrieving it as stream
     */
    internal static async Task<string> SafeReadContent(this HttpContent content, int maxLength = 8192) {
      if(content == null) // || content.Headers.ContentLength == null || content.Headers.ContentLength == 0)
        return null;
      var len = content.Headers.ContentLength;
      if (len > maxLength)
        return "(content too long)";
      try {
        var stream = await content.ReadAsStreamAsync();
        stream.Position = 0;
        var reader = new StreamReader(stream);
        var result = reader.ReadToEnd();
        // Important - in some cases if we don't reset position, the response stream is empty on the wire
        // Discovered it when playing with Swagger. We ignore swagger requests in this handler now, 
        // but resetting position needs to be done. 
        stream.Position = 0; 
        return result; 
      } catch (Exception ex) {
        return "(Read content failed); Error: " + ex.Message;
      }
    }

    //Converts URL to "standard" local path form - start with "/"
    internal static string AdjustPath(string path) {
      if(path == null) return path;
      if(!path.StartsWith("/")) path = "/" + path;
      if(path.EndsWith("/")) path = path.Substring(0, path.Length - 1);
      return path;
    }

    public static string AsString(this ModelStateDictionary modelStateDict) {
      if(modelStateDict == null)
        return null;
      if(modelStateDict.IsValid)
        return "IsValid:true";
      return "IsValid:false" + Environment.NewLine +
        string.Join(Environment.NewLine, modelStateDict.Select(kv => string.Format("{0}:{1}", kv.Key, kv.Value.AsString())));
    }
    public static string AsString(this ModelState modelState) {
      if(modelState == null)
        return null;
      var nl = Environment.NewLine;
      var value = modelState.Value;
      string valueStr = (value == null) ? null : string.Format("{0}->{1}", value.RawValue, value.AttemptedValue) + nl;
      if(modelState.Errors == null || modelState.Errors.Count == 0)
        return valueStr;
      //Add errors
      var result = valueStr + nl + "Exceptions:" + nl +
        string.Join(nl, modelState.Errors.Select(e => e.ErrorMessage + Environment.NewLine + e.Exception.ToLogString()));
      return result;
    }

    public static string Encrypt(string value, string purpose = "Generic") {
      var bytes = Encoding.Unicode.GetBytes(value);
      var encrBytes = MachineKey.Protect(bytes, purpose);
      var result = HexUtil.ByteArrayToHex(encrBytes);
      return result; 
    }
    public static string Decrypt(string value, string purpose = "Generic") {
      var bytes = HexUtil.HexToByteArray(value);
      var textBytes = MachineKey.Unprotect(bytes, purpose);
      var result = Encoding.Unicode.GetString(textBytes);
      return result;
    }

    public static long[] DecryptVersionArray(string token) {
      var content = Decrypt(token, "Versions");
      if(string.IsNullOrEmpty(content))
        return null;
      try {
        var arr = content.SplitNames(',').Select(s => long.Parse(s)).ToArray();
        return arr;
      } catch {  return null;  }
    }

    public static string EncryptVersionArray(long[] array) {
      return Encrypt(string.Join(",", array), "Versions");
    }

    public static string GetSwaggerApiGroup(this HttpActionDescriptor descriptor) {
      var slimDescr = descriptor as SlimApiActionDescriptor;
      if(slimDescr != null)
        return slimDescr.ControllerInfo.ApiGroup;
      return descriptor.ControllerDescriptor.ControllerType.Name;
    }

    public static string GetSwaggerOperationId(this HttpActionDescriptor descriptor) {
      string contrTypeName;
      var slimDescr = descriptor as SlimApiActionDescriptor;
      if(slimDescr != null)
        contrTypeName = slimDescr.ControllerInfo.TypeInfo.Name;
      else
        contrTypeName = descriptor.ControllerDescriptor.ControllerType.Name;
      return contrTypeName  + "_" + descriptor.ActionName;
    }

    public static bool IsSet(this WebHandlerOptions options, WebHandlerOptions option) {
      return (options & option) != 0;
    }

    public static bool IsSet(this WebTokenDirection flags, WebTokenDirection flag) {
      return (flags & flag) != 0;
    }


  }
}
