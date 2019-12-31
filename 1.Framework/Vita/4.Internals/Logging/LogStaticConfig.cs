using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Services;

namespace Vita.Entities.Logging {

  public static class LogStaticConfig {
    public static int BatchSize = 100;
    public static TimerInterval BatchingTimerInterval = TimerInterval.T_500_Ms;
  }
}
