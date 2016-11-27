using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.UnitTests.Web {
  // Top-level handler, added to Http pipeline, used for testing async calls. 
  // AsyncServer method in SpecialMethodsController verifies that method completes when CallStatus == "Returned" 
  class DiagnosticsHttpHandler : DelegatingHandler {

    public static string CallStatus; 
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
      CallStatus = "Started";
      var task = base.SendAsync(request, cancellationToken);
      CallStatus = "Returned";
      return task; 
    }
  }
}
