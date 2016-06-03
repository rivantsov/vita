using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.OAuthClient.Internal;
using Vita.Modules.WebClient;

namespace Vita.Modules.OAuthClient {


  public interface IOAuthClientService {
    IOAuthRemoteServerAccount GetOAuthAccount(OperationContext context, 
         OAuthServerType serverType, string serverName, Guid? ownerId = null);
    IOAuthClientFlow BeginOAuthFlow(OperationContext context, IOAuthRemoteServerAccount account);
    IOAuthClientFlow OnRedirected(OperationContext context, OAuthRedirectParams redirectParams);
    Task<IOAuthRemoteServerAccessToken> RetrieveAccessToken(OperationContext context, IOAuthClientFlow flow);
    Task<IOAuthRemoteServerAccessToken> RefreshAccessToken(IOAuthRemoteServerAccessToken accessToken);
    OpenIdToken UnpackJwtToken(string jwtToken); 
    void PrepareWebClient(IOAuthRemoteServerAccessToken token, WebApiClient client);


  }
}
