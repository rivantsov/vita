using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Common {

  public static class StringHelper {
    public static string Quote(this string value) {
      if (value == null) return "''";
      return "'" + value.Replace("'", "''") + "'";
    }

    public static string FirstCap(this string value) {
      if (value == null || value.Length == 0 || char.IsUpper(value[0])) return value;
      return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    public static string TrimSuffix(this string value, string suffix) {
      if (value == null || suffix == null || value.Length <= suffix.Length)
        return value;
      if (value.EndsWith(suffix, StringComparison.InvariantCultureIgnoreCase))
        return value.Substring(0, value.Length - suffix.Length);
      else
        return value;
    }

    public static string FormatUri(this string template, params object[] args) {
      if (args == null || args.Length == 0)
        return string.Format(template, args); //still call Format to catch missing args
      var sArgs = args.Select(a => EscapeForUri(a)).ToArray(); //escape
      return string.Format(template, sArgs);
    }

    public static string SafeFormat(this string message, params object[] args) {
      if (args == null || args.Length == 0)
        return message;
      try {
        return string.Format(CultureInfo.InvariantCulture, message, args);
      } catch (Exception ex) {
        return message + " (System error: failed to format message. " + ex.Message + ")";
      }
    }

    /// <summary>Safely splits the string and trims spaces from elements.</summary>
    /// <param name="value"></param>
    /// <param name="separators"></param>
    /// <returns></returns>
    public static string[] SplitNames(this string value, params char[] separators) {
      if (string.IsNullOrWhiteSpace(value))
        return new string[] { };
      if (separators == null || separators.Length == 0)
        separators = new char[] { ',', ';' };
      return value.Split(separators).Select(s => s.Trim()).ToArray();
    }

    public static string Pluralize(string name) {
      if (name.EndsWith("y"))
        return name.Substring(0, name.Length - 1) + "ies";
      if (name.EndsWith("s"))
        return name + "es";
      return name + "s";
    }

    public static string Unpluralize(string name) {
      if (name.EndsWith("ies")) // Categories
        return name.Substring(0, name.Length - 3) + "y";
      if (name.EndsWith("s")) //Orders
        return name.Substring(0, name.Length - 1);
      return name;
    }

    public static string EscapeForUri(object value) {
      return value == null ? string.Empty : Uri.EscapeDataString(value.ToString());
    }

    public static string EscapeForHtml(object value) {
      if (value == null)
        return string.Empty;
      // see:  http://weblogs.sqlteam.com/mladenp/archive/2008/10/21/Different-ways-how-to-escape-an-XML-string-in-C.aspx
      return System.Security.SecurityElement.Escape(value.ToString());
    }

    public static string[] EscapeManyForUri(object[] values) {
      if (values == null)
        return new string[] { };
      return values.Select(v => EscapeForUri(v)).ToArray();
    } 


  }//class
} //ns
