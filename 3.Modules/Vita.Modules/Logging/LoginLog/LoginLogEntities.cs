using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Logging {

  [Entity, DoNotTrack]
  public interface ILoginLog : ILogEntityBase {
    //No direct ref, only ID - to allow keeping records about deleted logins
    Guid? LoginId { get; set; }

    [Size(30)]
    string EventType { get; set; } //usually LoginEventType enum values as string

    [Size(Sizes.Description, options: SizeOptions.AutoTrim), Nullable]
    string Notes { get; set; }
  }


}
