using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Modules.OAuthClient.Internal;

namespace Vita.Modules.OAuthClient {

  public class OAuthClientSettings {
    public string RedirectUrlBase;
    public string EncryptionChannel;
    public IJsonDeserializer JsonDeserializer; // for Jwt token in OpenId connect
    public event AsyncEvent<OAuthRedirectEventArgs> Redirected; 

    public OAuthClientSettings(string redirectUrlBase = null) {
      RedirectUrlBase = redirectUrlBase; //it might be set later
    }

    internal async Task OnRedirected(object source, OAuthRedirectEventArgs args) {
      var evt = Redirected; 
      if (evt != null)
        await Redirected.RaiseAsync(source, args);
    }
  }//settings class

  #region OAuthRedirectEventArgs 
  /// <summary>EventArgs for redirect event in OAuthClientSettings.</summary>
  public class OAuthRedirectEventArgs : EventArgs {
    public readonly OperationContext Context;
    public readonly OAuthRedirectParams RedirectParams; 
    public readonly IOAuthClientFlow Flow;
    //Might be set by event handler
    public string PostRedirectTo; //can be set by event handler
    //public OAuthAccessTokenInfo Token;  // usually null; if handler retrieves the token, it is returned as a result of HandleRedirect 
    public OAuthRedirectEventArgs(OperationContext context, OAuthRedirectParams redirectParams, IOAuthClientFlow flow) {
      Context = context;
      RedirectParams = redirectParams;
      Flow = flow; 
    }

  }// class
  #endregion 

}//ns
