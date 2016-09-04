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
    IOAuthAccessToken GetUserOAuthToken(IEntitySession session, Guid userId, string serverName, string accountName = null);

    Task OnRedirected(OperationContext context, string state, string authCode, string error);
    event AsyncEvent<RedirectEventArgs> Redirected;
    Task<Guid> RetrieveAccessTokenAsync(OperationContext context, Guid flowId);
    Task<bool> RefreshAccessTokenAsync(OperationContext context, Guid tokenId);
    Task RevokeAccessTokenAsync(OperationContext context, Guid tokenId);
    void UpdateTokenStatus(IEntitySession session, Guid tokenId, OAuthTokenStatus status);

    void SetupOAuthClient(WebApiClient client, IOAuthAccessToken token);
    Task<string> GetBasicProfileAsync(OperationContext context, Guid tokenId);
    Task<TProfile> GetBasicProfileAsync<TProfile>(OperationContext context, Guid tokenId);
  }
}
