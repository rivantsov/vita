using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Api;

namespace Vita.Web {

  public class VersionTokenHandler : WebTokenHandler {
    public VersionTokenHandler(string name = "X-Versions") : base(name, WebTokenType.Header, WebTokenDirection.InputOutput) {
    }

    public override void HandleRequest(WebCallContext context, HttpRequestMessage request) {
      var versions = GetIncomingValue(context);
      if (string.IsNullOrWhiteSpace(versions))
        return;
      //Parse
      var arrVersions = versions.Split(',');
      if (arrVersions.Length < 1)
        return;
      int value;
      if (int.TryParse(arrVersions[0], out value))
        context.MinUserSessionVersion = value;
      if (arrVersions.Length > 1 && int.TryParse(arrVersions[1], out value))
        context.MinCacheVersion = value; 
    }

    public override void HandleResponse(WebCallContext context, HttpResponseMessage response) {
      var versions = context.MinUserSessionVersion + "," + context.MinCacheVersion;
      context.OutgoingHeaders.Add(this.TokenName, versions);
    }
  }//class
}
