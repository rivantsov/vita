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
    Task<Guid> RetrieveAccessToken(OperationContext context, Guid flowId);
    Task<bool> RefreshAccessToken(OperationContext context, Guid tokenId);

    void SetupOAuthClient(WebApiClient client, IOAuthAccessToken token);
    Task<string> GetBasicProfile(OperationContext context, Guid tokenId);
    Task<TProfile> GetBasicProfile<TProfile>(OperationContext context, Guid tokenId);
  }
}
