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

  public class BackgroundSaveEventArgs : EventArgs {
    public readonly IList<object> Entries; 
    public BackgroundSaveEventArgs(IList<object> entries) {
      Entries = entries; 
    }
  }

  public interface IBackgroundSaveService {
    void RegisterObjectHandler(Type objectType, IObjectSaveHandler saver);
    void AddObject(object item);
    event EventHandler<BackgroundSaveEventArgs> Saving;
    // temporarily suspends service
    IDisposable Suspend(); 
  }

}
