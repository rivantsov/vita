using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common; 
using Vita.Entities;
using Vita.Modules.OAuthClient.Internal;
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
    IOAuthRemoteServerAccount GetOAuthAccount(IOAuthRemoteServer server, string accountName, Guid? ownerId = null);
    IOAuthClientFlow BeginOAuthFlow(IOAuthRemoteServerAccount account, string scopes = null);
    Task OnRedirected(OperationContext context, string state, string authCode, string error);
    Task<IOAuthAccessToken> RetrieveAccessToken(IOAuthClientFlow flow);
    Task<IOAuthAccessToken> RefreshAccessToken(IOAuthAccessToken accessToken);
    void SetupWebClient(WebApiClient client, IOAuthAccessToken token);
    event AsyncEvent<RedirectEventArgs> Redirected;

  }
}
