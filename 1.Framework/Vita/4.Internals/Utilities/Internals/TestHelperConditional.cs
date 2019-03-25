using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Vita.Entities.Utilities.Internals {

  /// <summary>Static class to assist with perf testing. All methods marked with Conditional attribute,
  /// so all calls will disappear in Release build. </summary>
  internal static class TestHelperConditional {
    static int _counter;
    static bool _enablled;

    [System.Diagnostics.Conditional("DEBUG")]
    public static void Enable(bool enable = true) {
      _enablled = enable;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void RandomYield(int chance = 5) {
      if (!_enablled)
        return;
      var counter = Interlocked.Increment(ref _counter);
      if (counter % chance == 0)
        Thread.Yield();
    }
  }

}
