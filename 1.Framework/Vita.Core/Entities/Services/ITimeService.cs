using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Vita.Entities;
using Vita.Entities.Runtime;

namespace Vita.Entities.Services {

  /// <summary> Provides Time data for application. Allows shifting current time for testing purposes. </summary>
  /// <remarks> <para>All code in VITA framework requests current time through a globally available time service, not from <c>DateTime.Now</c> directly.
  /// The goal is to allow easy unit testing of time-sensitive functionality. The test code can easily shift current time to test any functionality 
  /// involving current time. </para>
  /// Additionally time service provides a millisecond-resolution tick counter based on StopWatch; this counter may be used to 
  /// measure execution time of operations. Note that absolute value of EllapsedMilliseconds property is meaningless, only differences matter. 
  /// </remarks>
  public interface ITimeService {
    DateTime UtcNow { get; }
    DateTime Now { get; }
    TimeSpan CurrentOffset { get; }
    long ElapsedMilliseconds { get; }
    void SetCurrentOffset(TimeSpan offset); //For testing

  }

}
