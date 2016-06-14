using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Vita.Common;
using Vita.Entities; 
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Modules.OAuthClient;
using Vita.Modules.EncryptedData; 

namespace Vita.Samples.OAuthDemoApp {
  public partial class MainForm : Form {
    IEntitySession _session;
    IOAuthClientService _service; 
     
    public MainForm() {
      InitializeComponent();
    }
    protected override void OnLoad(EventArgs e) {
      base.OnLoad(e);
      _session = Program.App.OpenSession();
      _service = Program.App.OAuthService;
      _service.Redirected += OAuthClientService_Redirected;
      //fill-in the box
      var servers = _session.EntitySet<IOAuthRemoteServer>().ToList();
      cboServers.Items.Clear();
      foreach(var srv in servers)
        cboServers.Items.Add(srv.Name);
      if(cboServers.Items.Count > 0)
        cboServers.SelectedIndex = 0;

    } //method

    Guid _aouthFlowId; 

    private Task OAuthClientService_Redirected(object source, RedirectEventArgs args) {
      this.InvokeAction(() => HandleRedirect(args));
      return null; //sync return
    }

    private void InvokeAction(Action action) {
      this.Invoke(action);
    }
    private void HandleRedirect(RedirectEventArgs args) {
      _aouthFlowId = Guid.Empty; 
      var flow = _session.GetEntity<IOAuthClientFlow>(args.FlowId);
      EntityHelper.RefreshEntity(flow); //make sure it is refreshed from DB
      switch(flow.Status) {
        case OAuthClientProcessStatus.Authorized:
          _aouthFlowId = args.FlowId; 
          lblStatus.Visible = true;
          lblStatus.Text = "Success, recieved redired with authorization code. Requesting access token....";
          btnGetToken.Enabled = true;
          break;
        case OAuthClientProcessStatus.Error:
          lblStatus.Visible = true;
          lblStatus.Text = "Error: " + flow.Error;
          break;
      }
    } //method


    private void cboServers_SelectedIndexChanged(object sender, EventArgs e) {
      if (!txtClientId.ReadOnly)
        EndEdit();
      txtClientId.Text = "";
      txtClientSecret.Text = "";
      var server = GetCurrentServer(); 
      if (server != null) {
        var act = _service.GetOAuthAccount(server, server.Name);
        if (act == null) {
          BeginEdit();
        } else {
          txtClientId.Text = act.ClientIdentifier;
          txtClientSecret.Text = act.ClientSecret.DecryptString();
          EndEdit(); 
        }
      }
    }

    private IOAuthRemoteServer GetCurrentServer() {
      return _service.GetOAuthServer(_session, (string)cboServers.SelectedItem);
    }

    private void BeginEdit() {
      txtClientId.ReadOnly = false;
      txtClientSecret.ReadOnly = false;
      btnEditSave.Text = "Save";
      txtClientId.Focus(); 
    }

    private void EndEdit() {
      txtClientId.ReadOnly = true;
      txtClientSecret.ReadOnly = true;
      btnEditSave.Text = "Edit";
      btnStart.Enabled = !string.IsNullOrWhiteSpace(txtClientId.Text); 
    }

    private void btnEditSave_Click(object sender, EventArgs e) {
      if(txtClientId.ReadOnly)
        BeginEdit();
      else
        EndEditClientInfo();
    }
    private void EndEditClientInfo() { 
      var clientId = txtClientId.Text;
      var secret = txtClientSecret.Text;
      if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret)) {
        MessageBox.Show("Please enter valid client ID and secret.");
        return; 
      }
      var server = GetCurrentServer();
      var acct = _service.GetOAuthAccount(server, server.Name); 
      if (acct == null) {
        acct = server.NewOAuthAccount(server.Name, null, clientId, secret); 
      } else {
        acct.ClientIdentifier = clientId;
        _session.NewOrUpdate(acct.ClientSecret, secret);  
      }
      _session.SaveChanges();
      EndEdit();
      btnStart.Enabled = true; 
        
    }//method

    private void btnStart_Click(object sender, EventArgs e) {
      var server = GetCurrentServer();
      var act = Program.App.OAuthService.GetOAuthAccount(server, server.Name);
      var flow = _service.BeginOAuthFlow(act, server.Scopes);
      _session.SaveChanges();
      System.Diagnostics.Process.Start(flow.AuthorizationUrl);

    }

    Guid _tokenId = Guid.Empty; 
    private async void btnGetToken_Click(object sender, EventArgs e) {
      _session = Program.App.OpenSession();
      lblStatus.Text = "Retrieving access token...";
      var flow = _session.GetEntity<IOAuthClientFlow>(_aouthFlowId);
      var token = await _service.RetrieveAccessToken(flow);
      _session.SaveChanges();
      _tokenId = token.Id; 
      var strToken = token.AuthorizationToken.DecryptString(); 
      lblStatus.Text = "Token retrieved: " + strToken;
      btnTestCall.Enabled = true; 
    }

    private void btnTestCall_Click(object sender, EventArgs e) {
      var token = _session.GetEntity<IOAuthAccessToken>(_tokenId);
      var callInfo = TestApiCalls.MakeTestApiCall(_service, token);
      if (callInfo != null)
        lblStatus.Text = "Response: " + callInfo.ResponseData;
    }
  }//class
}
