using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Linq;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data {

  public interface IDataStore {
    IList<EntityRecord> ExecuteSelect(EntityCommand command, EntitySession session, object[] args);
    void SaveChanges(EntitySession session);
    object ExecuteLinqCommand(LinqCommand command, EntitySession session);
    DataConnection GetConnection(EntitySession session, bool admin = false);
  }


}
