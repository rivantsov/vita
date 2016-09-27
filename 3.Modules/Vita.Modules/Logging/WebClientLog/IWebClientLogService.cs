using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.WebClient;

namespace Vita.Modules.Logging {

  public interface IWebClientLogService {
    void Log(OperationContext context, long duration, string urlTemplate, object[] urlArgs, 
         HttpRequestMessage request, HttpResponseMessage response, 
         SerializedContent requestContent, SerializedContent responseContent,
         Exception exception);
  }
}
