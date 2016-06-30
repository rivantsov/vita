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
    public readonly OperationContext Context; 
    public readonly Guid FlowId;
    public RedirectEventArgs(OperationContext context, Guid requestId) {
      Context = context; 
      FlowId = requestId;
    }
  }

  public interface IOAuthClientService {
    Task OnRedirected(OperationContext context, string state, string authCode, string error);
    event AsyncEvent<RedirectEventArgs> Redirected;

    Task<IOAuthAccessToken> RetrieveAccessToken(IOAuthClientFlow flow);
    Task<bool> RefreshAccessToken(IOAuthAccessToken accessToken);

    Task<TProfile> GetBasicProfile<TProfile>(IOAuthAccessToken token);
    IOAuthAccessToken GetUserOAuthToken(IEntitySession session, string serverName, string accountName = null); 
    void SetupWebClient(WebApiClient client, IOAuthAccessToken token);
    Task<string> GetBasicProfile(IOAuthAccessToken token);
    string ExtractUserId(IOAuthRemoteServer server, string profileJson); 
  }
}
