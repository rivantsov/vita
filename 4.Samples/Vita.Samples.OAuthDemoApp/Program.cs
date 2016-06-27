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
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main() {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
      Application.Run(new MainForm());
    }


  }//class
}
