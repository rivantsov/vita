using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Web.Http.SelfHost;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Modules.OAuthClient;
using Vita.Modules.EncryptedData;
using Vita.Modules.WebClient;
using Vita.Modules.WebClient.Sync;
using Vita.Web;

namespace Vita.Samples.OAuthDemoApp {

  public partial class MainForm : Form {
    OAuthEntityApp _app; 
    IOAuthClientService _service;
    HttpSelfHostServer _redirectServer;

    public MainForm() {
      InitializeComponent();
    }
    protected override void OnLoad(EventArgs e) {
      base.OnLoad(e);
      if(InitApp()) {
        //fill-in the Servers box
        LoadServersCombo();
        if(cboServers.Items.Count > 0)
          cboServers.SelectedIndex = 0;
      }
    } //method

    private void cboServers_SelectedIndexChanged(object sender, EventArgs e) {
      if (!txtClientId.ReadOnly)
        EndEditClientInfo();
      txtClientId.Text = "";
      txtClientSecret.Text = "";
      var session = OpenSession();
      var server = GetCurrentServer(session); 
      if (server != null) {
        linkDocs.Text = server.DocumentationUrl;
        var act = _service.GetOAuthAccount(server);
        if (act == null) {
          BeginEditClientInfo();
        } else {
          txtClientId.Text = act.ClientIdentifier;
          txtClientSecret.Text = act.ClientSecret.DecryptString();
          EndEditClientInfo(); 
        }
      }
    }

    private void btnEditSave_Click(object sender, EventArgs e) {
      if(txtClientId.ReadOnly)
        BeginEditClientInfo();
      else
        SaveClientInfo();
    }
    private void linkDocs_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
      System.Diagnostics.Process.Start(linkDocs.Text);
    }
    private void linkSpecialCases_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
      System.Diagnostics.Process.Start(linkSpecialCases.Text);
    }

    private void btnStart_Click(object sender, EventArgs e) {
      try {
        btnStart.Enabled = false;
        btnCancel.Enabled = true; 
        txtLog.Text = string.Empty;
        Application.DoEvents();
        RunOAuthProcess(); 
      } finally {
        btnStart.Enabled = true;
        btnCancel.Enabled = false;
      }
      _processStatus = ProcessStatus.Stopped;
    }

    private void btnCancel_Click(object sender, EventArgs e) {
      _processStatus = ProcessStatus.Stopped;
      Log("=== Canceled.");
    }

    private void Log(string template, params object[] args) {
      var text = args == null || args.Length == 0 ? template : string.Format(template, args);
      txtLog.Text += text + Environment.NewLine; 
    }
    private IEntitySession OpenSession() {
      return _app.OpenSession();
    }
    private IOAuthRemoteServer GetCurrentServer(IEntitySession session) {
      return _service.GetOAuthServer(session, (string)cboServers.SelectedItem);
    }

    #region Run OAuth process 
    enum ProcessStatus {
      Stopped,
      Executing,
      WaitingRedirect
    }
    ProcessStatus _processStatus;

    private async void RunOAuthProcess() {
      var session = OpenSession();
      var server = GetCurrentServer(session);
      Log("=== Starting OAuth2 flow, server: {0}", server.Name);
      var act = _service.GetOAuthAccount(server);
      var flow = _service.BeginOAuthFlow(act, null, server.Scopes);
      session.SaveChanges();
      _processStatus = ProcessStatus.WaitingRedirect;
      // Open browser page and direct it to authorization URL for the server
      // Note: actual authorization URL includes parameters
      Log("=== Opening server authorization page in Web Browser: {0} ...", server.AuthorizationUrl);
      Log("=== Waiting for the user action and redirect signal...");
      System.Diagnostics.Process.Start(flow.AuthorizationUrl);
      while(_processStatus == ProcessStatus.WaitingRedirect) {
        Application.DoEvents();
        System.Threading.Thread.Sleep(100);
      }
      if(_processStatus == ProcessStatus.Stopped)
        return;
      // Refresh flow entity and check for error
      EntityHelper.RefreshEntity(flow);
      if(flow.Status == OAuthFlowStatus.Error) {
        Log("=== Redirected, error: {0}", flow.Error);
        _processStatus = ProcessStatus.Stopped;
        return;
      }
      Log("=== Redirected; server returned authorization code: {0}", flow.AuthorizationCode);
      // Retrieve access token
      Log("=== Retrieving access token using authorization code...");
      var token = await _service.RetrieveAccessToken(flow);
      session.SaveChanges();
      var strToken = token.AccessToken.DecryptString();
      Log("=== Token retrieved: {0}", strToken);
      if (token.OpenIdToken != null) {
        Log("=== {0} supports OpenId Connect, so it also returned id_token. Inside id_token:", server.Name);
        Log("  Subject = {0}, expires(local) = {1}", token.OpenIdToken.Subject, token.OpenIdToken.ExpiresAt.ToLocalTime());
      }
      Log("=== Making a test call to server: GET {0}", server.BasicProfileUrl);
      var testResp = MakeTestApiCall(token);
      Log("=== Server response: ");
      Log(testResp);
      // We might not have refresh token - Google returns it only for the first access token request
      if (!string.IsNullOrEmpty(server.TokenRefreshUrl) && token.RefreshToken != null) {
        Log("=== Server supports refreshing tokens; refreshing token...");
        var success = await _service.RefreshAccessToken(token);
        Util.Check(success, "Failed to refresh token");
        var strToken2 = token.AccessToken.DecryptString();
        Log("=== Sucess, refreshed token (might be the same): {0}", strToken2);
      }
    }

    // Makes a test call to get-profile endpoint and returns json string
    private string MakeTestApiCall(IOAuthAccessToken token) {
      string testUrl = token.Account.Server.BasicProfileUrl;
      var testUri = new Uri(testUrl);
      var webClient = new WebApiClient(testUri.Scheme + "://" + testUri.Authority,
        ClientOptions.Default, typeof(string));
      _service.SetupWebClient(webClient, token);
      var respStream = webClient.ExecuteGet<System.IO.Stream>(testUri.AbsolutePath + testUri.Query);
      var reader = new StreamReader(respStream);
      var respText = reader.ReadToEnd();
      return respText;
    }//method


    // receives the signal from Redirect API controller that redirect was fired
    private Task OAuthClientService_Redirected(object source, RedirectEventArgs args) {
      if(_processStatus == ProcessStatus.WaitingRedirect)
        _processStatus = ProcessStatus.Executing;
      return null;
    }
    #endregion

    #region Edit client info
    private void BeginEditClientInfo() {
      txtClientId.ReadOnly = false;
      txtClientSecret.ReadOnly = false;
      btnEditSave.Text = "Save";
      txtClientId.Focus();
    }

    private void SaveClientInfo() {
      var clientId = txtClientId.Text;
      var secret = txtClientSecret.Text;
      if(string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret)) {
        MessageBox.Show("Please enter valid client ID and secret.");
        return;
      }
      var session = OpenSession();
      var server = GetCurrentServer(session);
      var acct = _service.GetOAuthAccount(server);
      if(acct == null) {
        acct = server.NewOAuthAccount(clientId, secret, "TestAccount");
      } else {
        acct.ClientIdentifier = clientId;
        session.NewOrUpdate(acct.ClientSecret, secret);
      }
      session.SaveChanges();
      EndEditClientInfo();
      btnStart.Enabled = true;
    }//method

    private void EndEditClientInfo() {
      txtClientId.ReadOnly = true;
      txtClientSecret.ReadOnly = true;
      btnEditSave.Text = "Edit";
      btnStart.Enabled = !string.IsNullOrWhiteSpace(txtClientId.Text);
    }

    #endregion 

    #region Initialization
    private bool InitApp() {
      Application.ThreadException += Application_ThreadException;
      try {
        var serviceURl = ConfigurationManager.AppSettings.Get("serviceUrl");
        _app = new OAuthEntityApp(serviceURl);
        _app.Init();
        // Get IOAuthClientService and hook to it's Redirected event - to get notified when user hits "Allow"
        _service = _app.GetService<IOAuthClientService>();
        _service.Redirected += OAuthClientService_Redirected;
        //Setup default encryption channel - we store OAuth data encrypted
        var cryptoKey = ConfigurationManager.AppSettings.Get("CryptoKey");
        var encrService = _app.GetService<Vita.Modules.EncryptedData.IEncryptionService>();
        encrService.AddChannel(HexUtil.HexToByteArray(cryptoKey));
        //Connect to db
        var connString = ConfigurationManager.AppSettings.Get("MsSqlConnectionString");
        if(!CheckConnection(connString))
          return false; 
        var dbSettings = new DbSettings(new MsSqlDbDriver(), MsSqlDbDriver.DefaultMsSqlDbOptions, connString);
        _app.ConnectTo(dbSettings);
        //Start local web server to handle redirects back from OAuth server, after user approves access
        StartWebService(serviceURl);
        // Hook to global error log, to show exception when it happens in Redirect controller
        var errLog = _app.GetService<IErrorLogService>();
        errLog.ErrorLogged += ErrLog_ErrorLogged;
        //Just for ease of debugging this app, update servers definitions in database
        var session = OpenSession();
        Vita.Modules.OAuthClient.OAuthServers.CreateUpdatePopularServers(session);
        session.SaveChanges();
        return true;
      } catch(Exception ex) {
        Log(ex.ToLogString());
        MessageBox.Show(ex.Message, "Error");
        return false; 
      }
    }

    private bool CheckConnection(string connString) {
      var conn = new System.Data.SqlClient.SqlConnection(connString); 
      try {
        conn.Open();
        conn.Close();
        return true; 
      } catch(Exception ex) {
        MessageBox.Show("Failed to connect to database: " + ex.Message + "\r\nMake sure you create VitaOAuthDemo database",
          "Database connection error");
        return false;
      }
    }

    private void StartWebService(string baseAddress) {
      var config = new HttpSelfHostConfiguration(baseAddress);
      WebHelper.ConfigureWebApi(config, _app, LogLevel.Details,
        WebHandlerOptions.ReturnBadRequestOnAuthenticationRequired | WebHandlerOptions.ReturnExceptionDetails);
      config.MaxReceivedMessageSize = int.MaxValue;
      config.MaxBufferSize = int.MaxValue;
      //setup redirect URL for OAuth
      _redirectServer = new HttpSelfHostServer(config);
      Task.Run(() => _redirectServer.OpenAsync());
      Log("The service is running on URL: " + baseAddress);
    }

    private void LoadServersCombo() {
      var session = OpenSession();
      var servers = session.EntitySet<IOAuthRemoteServer>().ToList();
      cboServers.Items.Clear();
      foreach(var srv in servers)
        cboServers.Items.Add(srv.Name);
    }


    #endregion 

    #region Exception handling
    private void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e) {
      Log(e.Exception.ToLogString());
      MessageBox.Show(e.Exception.Message, "Error");
      //LogException(e.Exception); 
    }


    private void ErrLog_ErrorLogged(object sender, ErrorLogEventArgs e) {
      // this can come from non-UI thread, so we need to use Invoke
      var action = new Action(() => {
        Log(e.Exception.ToLogString());
      });
      this.Invoke(action);
      Application.DoEvents(); 
      MessageBox.Show(e.Exception.Message, "Error");
    }
    #endregion

  }//class
}
