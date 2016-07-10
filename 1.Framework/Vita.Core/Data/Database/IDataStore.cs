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
    IList<EntityRecord> ExecuteSelect(EntitySession session, EntityCommand command, object[] args);
    void SaveChanges(EntitySession session);
    object ExecuteLinqCommand(EntitySession session, LinqCommand command);
    DataConnection GetConnection(EntitySession session, bool admin = false);
  }


}
