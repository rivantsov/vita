using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Runtime;

namespace Vita.Entities.Services {

  public interface IObjectSaveHandler {
    void SaveObjects(IEntitySession session, IList<object> items); 
  }

  public class SaveEventArgs : EventArgs {
    public readonly IEntitySession Session;
    public SaveEventArgs(IEntitySession session) {
      Session = session; 
    }
  }

  public interface IBackgroundSaveService {
    void RegisterObjectHandler(Type objectType, IObjectSaveHandler saver);
    void AddObject(object item);
    event EventHandler<SaveEventArgs> Saving;
    // temporarily suspends service
    IDisposable Suspend(); 
  }

}
