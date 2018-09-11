using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Linq;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Model {
  // Temp placeholder/definition of cache interface; caching to be refactored in the future
  public interface IEntityCache {
    void Shutdown();
    bool TryExecuteSelect(EntitySession session, EntityCommand command, out object result);
    void OnCommandExecuted(EntitySession session, EntityCommand command, object result);
    void OnSavedChanges(EntitySession session); 
  }

}
