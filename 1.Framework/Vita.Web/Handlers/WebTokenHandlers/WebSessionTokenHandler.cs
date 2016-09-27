using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Web {

  public class WebSessionTokenHandler : WebTokenHandler {
    public WebSessionTokenHandler(string name, WebTokenType tokenType = WebTokenType.Header)
      : base(name, tokenType, WebTokenDirection.Input) {  }

    public override void HandleRequest(Entities.Web.WebCallContext context, System.Net.Http.HttpRequestMessage request) {
      const string BasicPrefix = "Basic ";
      const string BearerPrefix = "Bearer ";
      var token = GetIncomingValue(context);
      if(string.IsNullOrWhiteSpace(token))
        return;
      //Remove scheme prefix
      if(token.StartsWith(BasicPrefix, StringComparison.OrdinalIgnoreCase))
        token = token.Substring(BasicPrefix.Length);
      else
      if(token.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        token = token.Substring(BearerPrefix.Length);
      context.UserSessionToken = token; 
    }//method

  }//class
}
