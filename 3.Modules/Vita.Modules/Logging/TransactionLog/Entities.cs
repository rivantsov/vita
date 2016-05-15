using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.Logging {

  [Entity, Dynamic, DoNotTrack, ClusteredIndex("CreatedOn,Id")]
  public interface ITransactionLog : ILogEntityBase {

    int Duration { get; set; }
    int RecordCount { get; set; }

    [Nullable, Unlimited]
    //Contains list of refs in the form : EntityType/Operation/PK
    string Changes { get; set; }
  }
}
