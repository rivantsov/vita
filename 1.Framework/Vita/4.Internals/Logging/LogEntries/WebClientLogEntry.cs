using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Vita.Entities.Logging {

  // Not used currently
  public class WebClientLogEntry : LogEntry {
    public string ClientName;
    public int Duration;
    public Uri RequestUri;
    public string HttpMethod;
    public HttpStatusCode? HttpStatus;
    public string RequestHeaders;
    public string RequestBody;
    public string ResponseHeaders;
    public string ResponseBody;
    public Exception Exception;
    public Guid? ErrorLogId;
    public Guid? WebCallId;

    public WebClientLogEntry(LogContext logContext, string clientName, long duration, HttpRequestMessage request, HttpResponseMessage response, string requestBody, string responseBody,
         Exception exception = null) : base(logContext) {
      ClientName = clientName; 
      Duration = (int)duration;
      RequestUri = request?.RequestUri;
      HttpMethod = request?.Method.Method;
      RequestBody = requestBody;
      RequestHeaders = GetHeadersAsString(request);
      ResponseBody = responseBody;
      ResponseHeaders = GetHeadersAsString(response);
      HttpStatus = response?.StatusCode;
      Exception = exception;
    }

    public override string AsText() {
      return $"{HttpMethod} {RequestUri}";
    }


    //Returns request headers as string, but without some secure headers like  Authorization header
    // (which might contain user password) then web call log is a security leak.
    private static string GetHeadersAsString(HttpRequestMessage request) {
      if(request == null)
        return string.Empty; 
      //join headers - omit Authorization header
      var strings = request.Headers.Select(h => string.Format("{0}:{1}",
           h.Key, "Authorization".Equals(h.Key, StringComparison.OrdinalIgnoreCase) ? "(ValueOmitted)" : string.Join(",", h.Value))).ToList();
      //content headers
      if(request.Content != null && request.Content.Headers != null)
        strings.AddRange(request.Content.Headers.Select(h => string.Format("{0}:{1}", h.Key, string.Join(",", h.Value))));
      var result = string.Join(Environment.NewLine, strings);
      return result;
    }

    internal static string GetHeadersAsString(HttpResponseMessage response) {
      if(response == null)
        return string.Empty;
      //join headers - omit security related headers to avoid security leaks
      var strings = response.Headers.Select(h => string.Format("{0}:{1}", h.Key, string.Join(",", h.Value))).ToList();
      //content headers
      if(response.Content != null && response.Content.Headers != null)
        strings.AddRange(response.Content.Headers.Select(h => string.Format("{0}:{1}", h.Key, string.Join(",", h.Value))));
      var result = string.Join(Environment.NewLine, strings);
      return result;
    }
  }//class
}
