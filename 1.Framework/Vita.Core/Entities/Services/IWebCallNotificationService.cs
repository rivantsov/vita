using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Web;

namespace Vita.Entities.Services {
  public class WebCallEventArgs : EventArgs {
    public readonly WebCallContext WebContext;
    public WebCallEventArgs(WebCallContext webContext) {
      WebContext = webContext; 
    }
  }

  //Implemented by WebCallContextHandler in Vita.Web
  public interface IWebCallNotificationService {
    event EventHandler<WebCallEventArgs> WebCallStarting;
    event EventHandler<WebCallEventArgs> WebCallCompleting;
  }

  public interface IWebCallNotificationControlService {
    void OnWebCallStarting(WebCallContext webContext);
    void OnWebCallCompleting(WebCallContext webContext);
  }

}
