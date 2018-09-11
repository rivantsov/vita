using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace Vita.Entities {

  public static class Util {

    public static Exception Rethrow(Exception inner, string message, params object[] args) {
      var msg = SafeFormat(message, args);
      throw new Exception(msg, inner);
    }
    public static Exception Throw(string message, params object[] args) {
      var msg = SafeFormat(message, args);
      throw new Exception(msg);
    }
    public static void Check(bool condition, string message, params object[] args) {
      if(!condition)
        Throw(message, args);
    }
    public static Exception NotImplemented(string message = null, params object[] args) {
      message = message ?? "Method not implemented.";
      message = SafeFormat(message, args);
      throw new NotImplementedException(message);
    }


    public static void CheckNotEmpty(string value, string message, params object[] args) {
      if (string.IsNullOrWhiteSpace(value))
        Throw(message, args);
    }

    public static void CheckParam(object value, string parameterName) {
      Check(value != null, "Parameter {0} may not be null.", parameterName);
    }
    public static void CheckParamNotEmpty(string value, string parameterName) {
      Check(!string.IsNullOrEmpty(value), "Parameter {0} may not be null or empty string.", parameterName);
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

    public static void Each<T>(this IEnumerable<T> list, Action<T> action) {
      foreach (var elem in list)
        action(elem); 
    }

    public static bool ContainsAll<T>(this IList<T> list, IEnumerable<T> subList) {
      foreach (var t in subList)
        if (!list.Contains(t)) return false;
      return true; 
    }

    /// <summary>Gets precise time intervals in milliseconds </summary>
    public static long GetPreciseMilliseconds() {
        return Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency; 
    }

    internal static string GetFullAppPath(string fileName) {
      if(string.IsNullOrWhiteSpace(fileName))
        return null;
      else if(Path.IsPathRooted(fileName))
        return fileName;
      else {
        var dir = AppContext.BaseDirectory;
        return Path.Combine(dir, fileName);
      }
    }

    public static string SafeFormat(string message, params object[] args) {
      if(args == null || args.Length == 0)
        return message;
      try {
        return string.Format(CultureInfo.InvariantCulture, message, args);
      } catch(Exception ex) {
        return message + " (System error: failed to format message. " + ex.Message + ")";
      }
    }

    /// <summary>Adds value to Exception.Data dictionary.</summary>
    /// <param name="exception">The target exception.</param>
    /// <param name="name">Value (parameter) name.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="doNotUse">Do not use. Filled automatically with method name and prepended to value name in the dictionary.</param>
    public static void AddValue(this Exception exception, string name, object value, [CallerMemberName] string doNotUse = null) {
      if(exception == null)
        return;
      var varName = name;
      if(exception.Data.Contains(varName)) //if duplicate, use caller member name
        varName = doNotUse + "/" + name;
      string strValue;
      if(value == null)
        strValue = null;
      else if(value is string)
        strValue = (string)value;
      else 
        strValue = value.ToLogString();
      exception.Data[varName] = strValue;
    }



  }//class

}//namespace
