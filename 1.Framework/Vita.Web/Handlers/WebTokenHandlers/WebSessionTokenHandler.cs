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
      context.UserSessionToken = GetIncomingValue(context); 
    }//method

  }//class
}
