using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http;
using System.IO;
using Vita.Common;

namespace Vita.Modules {

  public static class HttpHelper {
    //Returns request headers as string, but without some secure headers like  Authorization header
    // (which might contain user password) then web call log is a security leak.
    internal static string GetHeadersAsString(this HttpRequestMessage request) {
      //join headers - omit Authorization header
      var strings = request.Headers.Select(h => string.Format("{0}:{1}",
           h.Key, "Authorization".Equals(h.Key, StringComparison.OrdinalIgnoreCase) ? "(ValueOmitted)" : string.Join(",", h.Value))).ToList();
      //content headers
      if(request.Content != null && request.Content.Headers != null)
        strings.AddRange(request.Content.Headers.Select(h => string.Format("{0}:{1}", h.Key, string.Join(",", h.Value))));
      var result = string.Join(Environment.NewLine, strings);
      return result;
    }

    internal static string GetHeadersAsString(this HttpResponseMessage response) {
      //join headers - omit security related headers to avoid security leaks
      var strings = response.Headers.Select(h => string.Format("{0}:{1}", h.Key, string.Join(",", h.Value))).ToList();
      //content headers
      if(response.Content != null && response.Content.Headers != null)
        strings.AddRange(response.Content.Headers.Select(h => string.Format("{0}:{1}", h.Key, string.Join(",", h.Value))));
      var result = string.Join(Environment.NewLine, strings);
      return result;
    }

    internal static async Task<string> SafeReadContent(this HttpContent content, int maxLength = 8192) {
      if(content == null) // || content.Headers.ContentLength == null || content.Headers.ContentLength == 0)
        return null;
      var len = content.Headers.ContentLength;
      if(len > maxLength)
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
      } catch(Exception ex) {
        return "(Read content failed); Error: " + ex.Message;
      }
    }


  }
}
