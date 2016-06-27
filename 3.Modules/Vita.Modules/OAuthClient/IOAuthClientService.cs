using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common; 
using Vita.Entities;
using Vita.Modules.WebClient;

namespace Vita.Modules.OAuthClient {

  public class RedirectEventArgs : EventArgs {
    public Guid FlowId;
    public RedirectEventArgs(Guid requestId) {
      FlowId = requestId;
    }
  }

  public interface IOAuthClientService {
    IOAuthRemoteServer GetOAuthServer(IEntitySession session, string serverName);
    IOAuthRemoteServerAccount GetOAuthAccount(IOAuthRemoteServer server, string accountName = null);
    IOAuthClientFlow BeginOAuthFlow(IOAuthRemoteServerAccount account, Guid? userId = null, string scopes = null);
    Task OnRedirected(OperationContext context, string state, string authCode, string error);
    event AsyncEvent<RedirectEventArgs> Redirected;
    Task<IOAuthAccessToken> RetrieveAccessToken(IOAuthClientFlow flow);
    Task<bool> RefreshAccessToken(IOAuthAccessToken accessToken);
    IOAuthAccessToken GetUserOAuthToken(IEntitySession session, string serverName, string accountName = null); 
    void SetupWebClient(WebApiClient client, IOAuthAccessToken token);

  }
}
