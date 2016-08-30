using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Entities.Services.Implementations {

  //implementation
  internal class TimeService : ITimeService {
    private long _millisecondOffset;

    public TimeService() {  }

    #region ITimeService Members

    public DateTime UtcNow {
      get {
        var now = DateTime.UtcNow;
        return _millisecondOffset == 0 ? now : now.AddMilliseconds(_millisecondOffset);
      }
    }
    public DateTime Now {
      get {
        var now = DateTime.Now;
        return _millisecondOffset == 0 ? now : now.AddMilliseconds(_millisecondOffset);
      }
    }

    public TimeSpan CurrentOffset {
      get {
        return TimeSpan.FromMilliseconds(_millisecondOffset);
      }
    }

    public long ElapsedMilliseconds {
      get { return Util.PreciseTicks + _millisecondOffset; }
    }

    public void SetCurrentOffset(TimeSpan offset) {
      _millisecondOffset = (long) offset.TotalMilliseconds;
    }
    #endregion

  }//class

}
