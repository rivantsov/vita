using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Utilities {
  using System.Diagnostics;

  public interface ILastResortErrorLog {
    /// <summary>
    ///     Logs a fatal error. 
    /// </summary>
    void LogFatalError(string logSystemError, string originalError = null);
  }

  /// <summary>
  ///     The last-resort error log facility. Do NOT use it as a regular log in your application - use it only
  ///     when your regular log fails, and you need to save the info about the log failure, along with the original exception.
  /// </summary>
  /// <remarks>
  ///     Saves the errors to the application event log and writes to Trace output. Use the <see cref="Instance" />
  ///     singleton to access the log. You can replace the singleton with your own implementation if you need to.
  /// </remarks>
  public class LastResortErrorLog : ILastResortErrorLog {
    public const string EventLogName = "Application";
    public const int FatalEventId = 999;

    public static ILastResortErrorLog Instance { get; set; } = new LastResortErrorLog();

    public LastResortErrorLog() {
    }

    /// <summary>
    ///     Writes the exceptions information to the system Application event log and to the Trace output.
    /// </summary>
    /// <param name="logSystemError">The error of the log system.</param>
    /// <param name="originalError">The original error if the logger was trying to log an error. </param>
    public void LogFatalError(string logSystemError, string originalError = null) {
      var text = string.IsNullOrWhiteSpace(logSystemError) ? "(passed error message is empty)" : logSystemError;
      if (originalError != null) {
        text += "\r\nOriginal error: \r\n" + originalError;
      }

      Trace.WriteLine(text);

      using (EventLog eventLog = new EventLog(EventLogName)) {
        eventLog.Source = EventLogName; // Event source and log name are the same for Application log. 
        eventLog.WriteEntry(text, EventLogEntryType.Error, FatalEventId);
      }
    }

  }
}
