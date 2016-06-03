using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Modules.OAuthClient.Internal;
using Vita.Modules.WebClient;
using Vita.Modules.EncryptedData;

namespace Vita.Modules.OAuthClient {

  public partial class OAuthClientModule : IOAuthClientService {

    public IOAuthRemoteServerAccount GetOAuthAccount(OperationContext context, OAuthServerType serverType, string serverName, Guid? ownerId = default(Guid?)) {
      
    }

    public IOAuthClientFlow BeginOAuthFlow(OperationContext context, IOAuthRemoteServerAccount account) {
      var flow = account.NewOAuthFlow();
      return flow; 
    }

    public IOAuthClientFlow GetOAuthFlow(OperationContext context, Guid flowId) {
      var session = context.OpenSystemSession();
      var flow = session.GetEntity<IOAuthClientFlow>(flowId); 
      return flow;
    }

    public IOAuthClientFlow OnRedirected(IOAuthClientFlow flow, string authCode, string error) {
      var session = EntityHelper.GetSession(flow);
      // State contains FlowId
      context.ThrowIf(!Guid.TryParse(redirectParams.State, out flowId), ClientFaultCodes.InvalidValue,
        "state", "'state' parameter value expected to contain ID of OAuth flow. State: {0}.", redirectParams.State);

      context.ThrowIfNull(flow, ClientFaultCodes.ObjectNotFound, "state", "OAuth process does not exist, ID: {0}.", flowId);
      flow.AuthorizationCode = redirectParams.Code;
      return flow; 
    }

    public async Task<IOAuthRemoteServerAccessToken> RetrieveAccessToken(IOAuthClientFlow flow) {
      var webClient = new WebApiClient(flow.Account.Server.TokenRequestUrl, ClientOptions.Default);
      var tokenResp = await webClient.GetAsync<AccessTokenResponse>(OAuthTemplates.GetAccessTokenUrlQuery,
        flow.AuthorizationCode, flow.Account.ClientIdentifier, flow.Account.ClientSecret,
        flow.RedirectUrl);
      var expires = this.App.TimeService.UtcNow.AddSeconds(tokenResp.ExpiresIn);
      // Create AccessToken entity
      var accessToken = flow.Account.NewOAuthAccessToken(flow.UserId, tokenResp.AccessToken, tokenResp.RefreshToken,
         expires, Settings.EncryptionChannel); 
      // Unpack OpenId id_token - it is JWT token
      if (!string.IsNullOrWhiteSpace(tokenResp.IdToken)) {
        var idTkn = JwtDecoder.Decode(tokenResp.IdToken, Settings.JsonDeserializer);
        accessToken.NewOpenIdToken(idTkn);
      }
      var session = EntityHelper.GetSession(flow);
      session.SaveChanges(); 
      return accessToken;
    }

    public Task<IOAuthRemoteServerAccessToken> RefreshAccessToken(IOAuthRemoteServerAccessToken accessToken) {
      throw new NotImplementedException();
    }

    public void PrepareWebClient(IOAuthRemoteServerAccessToken token, WebApiClient client) {
      var tokenValue = token.AuthorizationToken.DecryptString(Settings.EncryptionChannel);
      client.AddAuthorizationHeader(tokenValue, scheme: token.TokenType.ToString());
    }


    // =========================================================== OLD =====================================
    public OAuthServerInfo GetOAuthServerInfo(OperationContext context, string serverName) {
      var session = context.OpenSystemSession();
      var info = session.EntitySet<IOAuthRemoteServer>().Where(s => s.Name == serverName).FirstOrDefault();
      return info.ToServerInfo(); 
    }

    public async Task<OAuthRedirectResult> HandleRedirect(OperationContext context, OAuthRedirectParams parameters) {
      var session = context.OpenSystemSession();
      Guid flowId;
      if(!Guid.TryParse(parameters.State, out flowId)) 
        return await RedirectError("Invalid State value: '{0}', expected Guid (flow ID).", parameters.State);
      var flow = session.GetEntity<IOAuthClientFlow>(flowId);
      if(flow == null)
        return await RedirectError("OAuth Flow not found, ID: {0}", flowId);
      if (!string.IsNullOrEmpty(parameters.Error)) {
        flow.Error = parameters.Error;
        flow.Status = OAuthClientFlowStatus.Error;
        session.SaveChanges();
        return await RedirectError("OAuth server returned error: {0}", parameters.Error);
      }
      //Update the flow
      flow.AuthorizationCode = parameters.Code; 
      var args = new OAuthRedirectEventArgs(context, parameters, flow);
      await this.Settings.OnRedirected(this, args);
      //If the token had been retrieved, return it
      if(args.Token != null) {
        session.SaveChanges();
        var result = new OAuthRedirectResult() { AccessToken = args.Token };
        return await Task.FromResult(result);
      }
      //Retreive access token
      var token = await RetrieveAccessToken(context, flow);
      return new OAuthRedirectResult() { AccessToken = token };
    }

    // Utilities 
    private static Task<OAuthRedirectResult> RedirectError(string message, params object[] args) {
      var result = new OAuthRedirectResult() { Error = StringHelper.SafeFormat(message, args) };
      return Task.FromResult(result);
    }

  } //class
}
