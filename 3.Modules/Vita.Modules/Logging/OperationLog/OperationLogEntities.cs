using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Logging {

  [Entity, Paged, DoNotTrack]
  public interface IOperationLog : ILogEntityBase {
    [Unlimited]
    string Message { get; set; }
  }

}//ns
