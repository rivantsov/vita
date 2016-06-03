using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vita.UnitTests.Common {
  public partial class BrowserForm : Form {
    public BrowserForm() {
      InitializeComponent();
      Browser.Navigated += Browser_Navigated;
    }

    private void Browser_Navigated(object sender, WebBrowserNavigatedEventArgs e) {
      this.Address.Text = e.Url.ToString();
    }
  }
}
