using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Web {
  public class WebCallEventArgs : EventArgs {
    public readonly WebCallContext WebContext;
    public WebCallEventArgs(WebCallContext webContext) {
      WebContext = webContext; 
    }
  }

  //Implemented by WebCallContextHandler
  public interface IWebCallNotificationService {
    event EventHandler<WebCallEventArgs> WebCallStarting;
    event EventHandler<WebCallEventArgs> WebCallCompleting;
  }

}
