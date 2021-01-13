using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Api;

namespace Vita.Entities.Services {

  public class WebCallEventArgs : EventArgs {
    public readonly WebCallContext WebContext;
    public WebCallEventArgs(WebCallContext webContext) {
      WebContext = webContext;
    }
  }

  //Implemented by WebCallContextHandler in Vita.Web
  /// <summary>Provides notification about web call start/end for web-based applications. </summary>
  public interface IWebCallNotificationService {
    event EventHandler<WebCallEventArgs> WebCallStarting;
    event EventHandler<WebCallEventArgs> WebCallCompleting;
  }

}
