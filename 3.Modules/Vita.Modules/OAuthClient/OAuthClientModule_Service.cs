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
using System.Net.Http;
using System.Net.Http.Headers;

namespace Vita.Modules.OAuthClient {

  public partial class OAuthClientModule : IOAuthClientService {

    // Standard response to get-access-token endpoint
    public class AccessTokenResponse {
      [Node("access_token")]
      public string AccessToken;
      [Node("expires_in")]
      public long ExpiresIn;
      [Node("token_type")]
      public string TokenType;
      [Node("refresh_token")]
      public string RefreshToken;
      [Node("id_token")]
      public string IdToken; //Open ID connect only
    }

    /// <summary>The query (parameters) portion of authorization URL - a page on OAuth server 
    /// that user is shown to approve the access by the client app. </summary>
    public const string AuthorizationUrlQuery = "?response_type=code&client_id={0}&redirect_uri={1}&scope={2}&state={3}";
    /// <summary>The query (parameters) portion; sent either as part of URL or as form-url-encoded body.</summary>
    public const string AccessTokenUrlQueryTemplate =
      "code={0}&client_id={1}&client_secret={2}&redirect_uri={3}&grant_type=authorization_code&access_type=offline";
    public const string OpenIdClaimsParameter = "claims={0}";

    #region IAuthClientService
    public event AsyncEvent<RedirectEventArgs> Redirected;

    public IOAuthRemoteServer GetOAuthServer(IEntitySession session, string serverName) {
      return session.EntitySet<IOAuthRemoteServer>().Where(s => s.Name == serverName).FirstOrDefault();
    }
  
    public IOAuthRemoteServerAccount GetOAuthAccount(IOAuthRemoteServer server, string accountName, Guid? ownerId = default(Guid?)) {
      var session = EntityHelper.GetSession(server); 
      var accountQuery = session.EntitySet<IOAuthRemoteServerAccount>().Where(a => a.Server == server && a.Name == accountName);
      if(ownerId != null)
        accountQuery = accountQuery.Where(a => a.OwnerId == ownerId.Value);
      var act = accountQuery.FirstOrDefault();
      return act;       
    }

    public IOAuthClientFlow BeginOAuthFlow(IOAuthRemoteServerAccount account, string scopes = null) {
      var flow = account.NewOAuthFlow();
      var redirectUrl = this.Settings.RedirectUrl;
      if(account.Server.Options.IsSet(OAuthServerOptions.TokenReplaceLocalIpWithLocalHost))
        redirectUrl = redirectUrl.Replace("127.0.0.1", "localhost"); //Facebook special case
      var clientId = account.ClientIdentifier;
      flow.Scopes = scopes ?? account.Server.Scopes; //all scopes
      flow.RedirectUrl = redirectUrl;
      flow.AuthorizationUrl = account.Server.AuthorizationUrl + StringHelper.FormatUri(AuthorizationUrlQuery, clientId, redirectUrl, flow.Scopes, flow.Id.ToString());
      return flow; 
    }

    public async Task OnRedirected(OperationContext context, string state, string authCode, string error) {
      var session = context.OpenSystemSession();
      Guid reqId;
      Util.Check(Guid.TryParse(state, out reqId), "Invalid state parameter ({0}), expected GUID.", state);
      var flow = session.GetEntity<IOAuthClientFlow>(reqId);
      Util.Check(flow != null, "OAuth Redirect: invalid state parameter, OAuth request not found.", state);
      flow.AuthorizationCode = authCode;
      flow.Error = error;
      flow.Status = string.IsNullOrWhiteSpace(error) ? OAuthClientProcessStatus.Authorized : OAuthClientProcessStatus.Error;
      session.SaveChanges(); 
      if(Redirected != null) {
        var args = new RedirectEventArgs(flow.Id);
        await Redirected.RaiseAsync(this, args);
      }
    }

    public async Task<IOAuthAccessToken> RetrieveAccessToken(IOAuthClientFlow flow) {
      string err = null;
      switch(flow.Status) {
        case OAuthClientProcessStatus.Started: err = "Access not authorized yet.";  break;
        case OAuthClientProcessStatus.TokenRetrieved: err = "Authorization code already used to retrieve token."; break;
        case OAuthClientProcessStatus.Error: err = "Authorization failed or denied - " + flow.Error;  break;           
      }
      Util.Check(err == null, "Cannot retrieve token: {0}.", err);
      Util.CheckNotEmpty(flow.AuthorizationCode, "Authorization code not retrieved, cannot retrieve access token.");

      var apiClient = new WebApiClient(flow.Account.Server.TokenRequestUrl, ClientOptions.Default, badRequestContentType: typeof(string));
      var clientSecret = flow.Account.ClientSecret.DecryptString(this.Settings.EncryptionChannel);
      var server = flow.Account.Server;
      var serverOptions = server.Options;
      // Some servers require specific authorization header
      if(serverOptions.IsSet(OAuthServerOptions.TokenUseAuthHeaderBasic64)) {
        var clientInfo = flow.Account.ClientIdentifier + ":" + clientSecret;
        var encClientInfo = StringHelper.Base64Encode(clientInfo);
        apiClient.AddAuthorizationHeader(encClientInfo, "Basic");
      }
      var query = StringHelper.FormatUri(AccessTokenUrlQueryTemplate, flow.AuthorizationCode, flow.Account.ClientIdentifier, clientSecret, flow.RedirectUrl);
      //Make a call; standard is POST, but some servers use GET (LinkedIn)
      AccessTokenResponse tokenResp;
      if(serverOptions.IsSet(OAuthServerOptions.TokenUseGet)) {
        //GET, no body
        tokenResp = await apiClient.GetAsync<AccessTokenResponse>("?" + query);
      } else {
        //POST
        if (serverOptions.IsSet(OAuthServerOptions.TokenUseFormUrlEncodedBody)) {
          //Form-encoded body
          var formContent = CreateFormUrlEncodedContent(query);
          tokenResp = await apiClient.PostAsync<HttpContent, AccessTokenResponse>(formContent, string.Empty);
        } else 
          tokenResp = await apiClient.PostAsync<object, AccessTokenResponse>(null, "?" + query);
      }
      flow.Status = OAuthClientProcessStatus.TokenRetrieved;
      //LinkedIn returns milliseconds here - it's a bug, reported. So here is workaround
      var expIn = tokenResp.ExpiresIn;
      if(expIn > 1e+9) //if more than one billion, it is milliseconds
        expIn = expIn / 1000;  
      var expires = this.App.TimeService.UtcNow.AddSeconds(expIn);
      // Create AccessToken entity
      var accessToken = flow.Account.NewOAuthAccessToken(flow.UserId, tokenResp.AccessToken,
          tokenResp.RefreshToken, tokenResp.IdToken, flow.Scopes, App.TimeService.UtcNow, expires, Settings.EncryptionChannel); 
      // Unpack OpenId id_token - it is JWT token
      if (serverOptions.IsSet(OAuthServerOptions.OpenIdConnect) && !string.IsNullOrWhiteSpace(tokenResp.IdToken)) {
        var payload = OpenIdConnectUtil.GetJwtPayload(tokenResp.IdToken);
        var idTkn = Settings.JsonDeserializer.Deserialize<OpenIdToken>(payload);
        accessToken.NewOpenIdToken(idTkn, payload);
      }
      return accessToken;
    }

    private static string FormatGetTokenQuery(string template, string authCode, string clientId, string clientSecret, string redirectUri) {
      var result = template
        .Replace("{code}", Uri.EscapeDataString(authCode))
        .Replace("{client_id}", Uri.EscapeDataString(clientId))
        .Replace("{client_secret}", Uri.EscapeDataString(clientSecret))
        .Replace("{redirect_uri}", Uri.EscapeDataString(redirectUri));
      return result; 
    }
    private static HttpContent CreateFormUrlEncodedContent(string content) {
      var bytes = Encoding.ASCII.GetBytes(content);
      var stream = new System.IO.MemoryStream(bytes); 
      HttpContent cnt = new StreamContent(stream);
      cnt.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
      return cnt; 
    }

    public Task<IOAuthAccessToken> RefreshAccessToken(IOAuthAccessToken accessToken) {
      throw new NotImplementedException();
    }

    public void SetupWebClient(WebApiClient client, IOAuthAccessToken token) {
      var tokenValue = token.AuthorizationToken.DecryptString(Settings.EncryptionChannel);
      client.AddAuthorizationHeader(tokenValue, scheme: token.TokenType.ToString());
    }
    #endregion 

  } //class
}
