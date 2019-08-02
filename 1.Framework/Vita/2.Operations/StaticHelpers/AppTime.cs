using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Vita.Entities {

  /// <summary>
  ///     A static utility class implementing the access to Now and UtcNow date/time system functions.
  /// </summary>
  /// <remarks>
  ///     The class provides wrappers around the <see cref="DateTime.UtcNow"/> and <see cref="DateTime.Now"/>
  ///     properties. The goal of the wrappers is to be able to shift/offset the current time
  ///     value in unit tests, when testing the time-dependent features, like data expiration.
  /// </remarks>
  public static class AppTime {
    private static TimeSpan _offset = TimeSpan.Zero;

    /// <summary>
    ///     Gets a System.DateTime object that is set to the current date and time on this
    ///     computer, expressed as the Coordinated Universal Time (UTC).
    ///     The returned value can be shifted by the unit testing code.
    /// </summary>
    public static DateTime UtcNow {
      get {
        var now = DateTime.UtcNow;
        return (_offset == TimeSpan.Zero) ? now : now.Add(_offset);
      }
    }

    /// <summary>
    ///     Gets a System.DateTime object that is set to the current date and time on this
    ///     computer, expressed as the local time.
    ///     The returned value can be shifted by the unit testing code.
    /// </summary>
    public static DateTime Now {
      get {
        var now = DateTime.Now;
        return (_offset == TimeSpan.Zero) ? now : now.Add(_offset);
      }
    }

    /// <summary>
    ///     Sets an offset to DateTime value returned by the class Now and UtcNow properties. For use in testing environment.
    /// </summary>
    /// <param name="offset">The time offset value.</param>
    /// <remarks>
    ///     As an example, calling this method with value of 2 hours effectively moves the current application time by
    ///     2 hours in the future. You can use negative offset value to shift time to the past. Use the <see cref="ClearOffset"/>
    ///     method to clear the offset (set it to zero). 
    /// </remarks>
    public static void SetOffset(TimeSpan offset) {
      _offset = offset;
    }

    /// <summary>
    ///     Clears the offset to DateTime value returned by the class Now and UtcNow properties. For use in testing environment.
    /// </summary>
    public static void ClearOffset() {
      _offset = TimeSpan.Zero;
    }

    public static long Timestamp {
      get { return Stopwatch.GetTimestamp(); } 
    }

    public static TimeSpan GetTimeSince(long fromTimestamp) {
      var tsNow = Stopwatch.GetTimestamp(); 
      var ticks = (tsNow - fromTimestamp);
      var time = TimeSpan.FromTicks(ticks);
      return time; 
    }

  } //class
}
