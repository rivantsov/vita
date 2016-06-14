using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Diagnostics;
using Vita.Common;
using Vita.Entities;
using Vita.Data;
using Vita.Data.MsSql;
using System.Web.Http.SelfHost;
using Vita.Web;
using Vita.Entities.Services;

namespace Vita.Samples.OAuthDemoApp {
  static class Program {
    public static OAuthEntityApp App;
    public static HttpSelfHostServer RedirectServer; 

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main() {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.ThreadException += Application_ThreadException;
      if (InitApp())
        Application.Run(new MainForm());
    }

    private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e) {
      Debug.WriteLine(e.Exception.ToLogString());
      MessageBox.Show(e.Exception.Message + "\r\nSee Debug output for details", "Error");
    }

    private static bool InitApp() {
      try {
        var serviceURl = ConfigurationManager.AppSettings.Get("serviceUrl");
        App = new OAuthEntityApp(serviceURl);
        App.Init();
        //Setup default encryption channel - we store OAuth data encrypted
        var cryptoKey = ConfigurationManager.AppSettings.Get("CryptoKey");
        var encrService = App.GetService<Vita.Modules.EncryptedData.IEncryptionService>();
        encrService.AddChannel(HexUtil.HexToByteArray(cryptoKey)); 
        //Connect to db
        var connString = ConfigurationManager.AppSettings.Get("MsSqlConnectionString");
        var dbSettings = new DbSettings(new MsSqlDbDriver(), MsSqlDbDriver.DefaultMsSqlDbOptions, connString);
        App.ConnectTo(dbSettings);
        //Start local web server to handle redirects
        StartService(serviceURl);
        // Hook to global error log, to show exception when it happens in Redirect controller
        var errLog = App.GetService<IErrorLogService>();
        errLog.ErrorLogged += ErrLog_ErrorLogged;
        //Just for ease of debugging this app, rerun server definitions
        UpdateOAuthServers(); 
        return true;
      } catch(Exception ex) {
        Debug.WriteLine(ex.ToLogString());
        MessageBox.Show(ex.Message + "\r\nSee Debug output for details. Make sure you create VitaOAuth database.", "Error");
        return false; 
      }
    }

    private static void ErrLog_ErrorLogged(object sender, ErrorLogEventArgs e) {
      Debug.WriteLine(e.Exception.ToLogString());
      MessageBox.Show(e.Exception.Message, "Error");
    }

    public static void StartService(string baseAddress) {
      var config = new HttpSelfHostConfiguration(baseAddress);
      WebHelper.ConfigureWebApi(config, App, LogLevel.Details,
        WebHandlerOptions.ReturnBadRequestOnAuthenticationRequired | WebHandlerOptions.ReturnExceptionDetails);
      config.MaxReceivedMessageSize = int.MaxValue;
      config.MaxBufferSize = int.MaxValue;
      //setup redirect URL for OAuth
      RedirectServer = new HttpSelfHostServer(config);
      Task.Run(() => RedirectServer.OpenAsync());
      Debug.WriteLine("The service is running on URL: " + baseAddress);
    }

    // update server definitions - just for debugging this demo app
    public static void UpdateOAuthServers() {
      var session = App.OpenSession(); 
      Vita.Modules.OAuthClient.OAuthServers.CreateUpdatePopularServers(session);
      session.SaveChanges(); 
    }
  }//class
}
