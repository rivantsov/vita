using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Threading;

using Vita.Common;
using Vita.Entities;
using Vita.UnitTests.Common;
using Vita.Modules.OAuthClient;
using Vita.Modules.WebClient.Sync;

namespace Vita.UnitTests.Web {

  public partial class WebTests  {

    //constants
    string _googleOAuthServer = "google-oauth2"; 
    string _googleAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    string _googleTokenRequestUrl = "https://www.googleapis.com/oauth2/v4/token";
    // Refresh: should be POST to this URL, with Form-encoded values
    string _googleTokenRefreshUrl = "https://www.googleapis.com/oauth2/v4/token";
    string _googleTokenRevokeUrl = "https://accounts.google.com/o/oauth2/revoke";

    string _redirectUrlTemplate = "http://127.0.0.1:3730/api/oauth_redirect/{0}";

    string _scopes = "profile";

// See:
    // https://developers.google.com/identity/protocols/OAuth2WebServer#preparing-to-start-the-oauth-20-flow
    // scroll to section Redirecting to Google's OAuth 2.0 server
    // there are more parameters there than we have here
    string _authUrlQueryTemplate = "response_type=code&client_id={0}&redirect_uri={1}&scope={2}&state={3}";
    //grant_type contains authorization_code
    string _tokenUrlQueryTemplate = "code={0}&client_id={1}&client_secret={2}& redirect_uri={3}&grant_type=authorization_code";

    private void CreateOAuthServerData() {
      var app = SetupHelper.BooksApp;
      var session = app.OpenSession();
      var googleOAuth = session.NewOAuthRemoteServer(_googleOAuthServer, OAuthServerType.OAuth2, _clientId, _clientSecret,
        _googleAuthUrl, _googleTokenRequestUrl, _googleTokenRefreshUrl);
      session.SaveChanges();    
    }

    [TestMethod]
    public void Test_OAuth() {
      var sessionId = 1;
      var redirectUrl = String.Format(_redirectUrlTemplate, _googleOAuthServer);
      var authUrl = _googleAuthUrl + "?" +
          StringHelper.FormatUri(_authUrlQueryTemplate, _clientId, redirectUrl, _scopes, sessionId);

      var browser = new BrowserHandler();
      browser.Open(); 
      browser.NavigateTo(authUrl);
      var doc = browser.GetDocument(); 
      Thread.Sleep(5000);
      var title = doc.Title;
      if (title.StartsWith("Sign in")) {
        var elEmail = doc.GetElementById("Email");
        elEmail.InnerText = _userGmail;
        var btnNext = doc.GetElementById("next");
        browser.Click(btnNext);
        doc = browser.GetDocument(); 
        var elPwd = doc.GetElementById("Passwd");
        elPwd.SetAttribute("value", _userGmailPwd);
        var btnSignin = doc.GetElementById("signIn");
        browser.Click(btnSignin);
      }
      doc = browser.GetDocument();
      var btnAllow = doc.GetElementById("submit_approve_access");
      browser.Click(btnAllow); 
      Thread.Sleep(15000);
      browser.Close(); 
    }
  }//class
}
