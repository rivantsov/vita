using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using System.Linq.Expressions;
using System.Diagnostics;

namespace Vita.Common {

  public static class Util {
    public static Exception Rethrow(Exception inner, string message, params object[] args) {
      var msg = message.SafeFormat(args);
      throw new Exception(msg, inner);
    }
    public static Exception Throw(string message, params object[] args) {
      var msg = message.SafeFormat(args);
      throw new Exception(msg);
    }
    public static Exception NotImplemented(string message = null, params object[] args) {
      message = message?? "Method not implemented.";
      message = message.SafeFormat(args); 
      throw new NotImplementedException(message); 
    }
    public static void Check(bool condition, string message, params object[] args) {
      if(!condition)
        Throw(message, args);
    }

    public static void CheckNotEmpty(string value, string message, params object[] args) {
      if (string.IsNullOrWhiteSpace(value))
        Throw(message, args);
    }

    public static void CheckAllNotNull(IEnumerable values, string message, params object[] args) {
      if (args == null)
        return; 
      var index = 0;
      var nullIndexes = new List<int>(); 
      foreach (var value in values) {
        if (value == null)
          nullIndexes.Add(index);
        index++;
      }
      if (nullIndexes.Count == 0) 
        return; 
      var msg = StringHelper.SafeFormat(message, args) + " (nulls at indexes " + string.Join(",", nullIndexes) + ")";
      Throw(message);
    }


    private static int _hashCount;
    public static int NewHash() {
      _hashCount++;
      var sHash = _hashCount + "_" + _hashCount;
      var code = sHash.GetHashCode();
      return code; 
    }

    public static DateTime CheckKind(this DateTime dateTime, DateTimeKind kindIfUndefined = DateTimeKind.Local) {
      if (dateTime.Kind != DateTimeKind.Unspecified)
        return dateTime;
      var result = new DateTime(dateTime.Ticks, kindIfUndefined);
      return result;
    }

    public static string CheckLength(string value, int maxLen) {
      if (string.IsNullOrEmpty(value)) return value;
      if (value.Length <= maxLen)
        return value;
      value = value.Substring(0, maxLen - 4) + " ...";
      return value; 
    }

    //.NET framework's GetHashCode() is not garanteed to be stable between .NET versions. 
    // If we want to keep hashes in database, we need a stable hash implementation 
    public static int StableHash(string value) {
      if (string.IsNullOrWhiteSpace(value))
        return 0; 
      return Crc32.Compute(value.Trim());
    }
    public static long StableHash64(this string value) {
      if(string.IsNullOrWhiteSpace(value))
        return 0L;
      return Crc32.Compute64(value.Trim());
    }

    public static void Each<T>(this IEnumerable<T> list, Action<T> action) {
      foreach (var elem in list)
        action(elem); 
    }

    public static bool ContainsAll<T>(this IList<T> list, IEnumerable<T> subList) {
      foreach (var t in subList)
        if (!list.Contains(t)) return false;
      return true; 
    }

    public static T[] EmptyArray<T>() {
      return new T[] { };
    }

    /// <summary>
    /// Gets precise time intervals in milliseconds 
    /// </summary>
    public static long PreciseTicks {
      get {
        return Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency; 
      }
    }

    public static void WriteToTrace(Exception exception, string localLog, bool copyToEventLog = false) {
      string message, details;
      if(exception == null) {
        details = message = "(No message, exception is null)";
      } else {
        message = exception.Message;
        details = exception.ToLogString();
      }
      WriteToTrace(message, details, localLog, copyToEventLog);
    }

    public static void WriteToTrace(string message, string details, string localLog, bool copyToEventLog = false) {
      var lines = new List<string>();
      lines.Add("===================== ERROR ============================");
      lines.Add(StringHelper.SafeFormat("User={0}", Environment.UserName));
      lines.Add(message);
      if(!string.IsNullOrWhiteSpace(details)) {
        lines.Add("DETAILS: ");
        lines.Add(details);
      }
      lines.Add(details);
      if(!string.IsNullOrEmpty(localLog))
        lines.Add(" Local log: \r\n" + localLog);
      lines.Add("END ERROR REPORT =======================================");
      var errText = string.Join(Environment.NewLine, lines);
      //Write to Trace
      Trace.WriteLine(errText);
      //Write to Windows event log
      if(copyToEventLog) {
        try { //under IIS creating event source might fail
          var srcName = "VITA_ERROR_LOG";
          if(!EventLog.SourceExists(srcName))
            EventLog.CreateEventSource(srcName, srcName);
          EventLog.WriteEntry(srcName, errText);
        } catch { }
      }
    }




  }//class

}//namespace
