using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Utilities {

  public static class StringHelper {
    public static string Quote(this string value) {
      if (value == null) return "''";
      if (value.Contains('\''))
        return "'" + value.Replace("'", "''") + "'";
      return "'" + value + "'";
    }

    public static string DoubleQuote(this string value) {
      if(value == null)
        return "\"\"";
      return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static string TrimLength(string s, int maxLength) {
      if(string.IsNullOrEmpty(s) || s.Length <= maxLength)
        return s;
      return s.Substring(0, maxLength - 4) + " ...";
    }

    public static string FirstCap(this string value) {
      if (value == null || value.Length == 0 || char.IsUpper(value[0])) return value;
      return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    public static string TrimSuffix(this string value, string suffix) {
      if (value == null || suffix == null || value.Length <= suffix.Length)
        return value;
      if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        return value.Substring(0, value.Length - suffix.Length);
      else
        return value;
    }

    public static string TrimEndingSemi(string value) {
      return value.TrimEnd(' ', '\r', '\n', ';');
    }


    public static string FormatUri(this string template, params object[] args) {
      if (args == null || args.Length == 0)
        return string.Format(template, args); //still call Format to catch missing args
      var sArgs = args.Select(a => EscapeForUri(a)).ToArray(); //escape
      return string.Format(template, sArgs);
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

    public static string[] EscapeManyForUri(object[] values) {
      if (values == null)
        return new string[] { };
      return values.Select(v => EscapeForUri(v)).ToArray();
    } 

    public static IDictionary<string, string> GetParameters(this Uri uri) {
      var dict = new Dictionary<string, string>();
      var kvArr = uri.Query.Split('&');
      foreach(var kv in kvArr) {
        var nv = kv.Split('=');
        var name = Uri.UnescapeDataString(nv[0]);
        var value = Uri.UnescapeDataString(nv[1]);
        dict[name] = value;
      }
      return dict; 
    }

    public static string Base64Encode(string plainText) {
      var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
      return System.Convert.ToBase64String(plainTextBytes);
    }
    public static string Base64Decode(string base64EncodedData) {
      var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
      return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }

    public static string ToCamelCase(this string value) {
      if(string.IsNullOrEmpty(value))
        return value;
      return Char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    public static string ToUnderscoreAllLower(this string value) {
      if(string.IsNullOrEmpty(value))
        return value;
      var chars = value.ToCharArray();
      char prevCh = '\0';  
      var newChars = new List<char>();
      foreach(var ch in chars) {
        if(char.IsUpper(ch)) {
          if(newChars.Count > 0 && prevCh != '_' && !char.IsUpper(prevCh)) //avoid double-underscores
            newChars.Add('_');
          newChars.Add(char.ToLowerInvariant(ch));
        } else
          newChars.Add(ch);
        prevCh = ch;
      }
      var result = new string(newChars.ToArray()).Replace("__", "_"); //cleanup double _, just in case
      return result;
    }

    // Helper method, allows using string list as log object
    public static string Add(this IList<string> list, string message, params object[] args) {
      var str = Util.SafeFormat(message, args);
      list.Add(str);
      return str; 
    }

    public static string ToText(this IList<string> list) {
      return string.Join(Environment.NewLine, list); 
    }

    /// <summary>Parses text template, convertes segmets like {name} to standard template wildcards like {0}, and returns name list as output argument.</summary>
    /// <param name="template"></param>
    /// <param name="adjustedTemplate"></param>
    /// <param name="argNames"></param>
    public static bool TryParseTemplate(string template, out string adjustedTemplate, out string[] argNames) {
      argNames = new string[] { };
      adjustedTemplate = template;
      try {
        if(string.IsNullOrWhiteSpace(template)) 
          return false;
        const char lbr = '{';
        const char rbr = '}';
        var chars = template.ToCharArray();
        var outBuilder = new StringBuilder();
        var nameList = new List<string>();
        var skipNext = false;
        var nameStart = -1;
        for(int i = 0; i < chars.Length; i++) {
          if(skipNext) {
            skipNext = false;
            continue;
          }
          var ch = chars[i];
          var chNext = i + 1 < chars.Length ? chars[i + 1] : '\0';
          // }} and {{ - escaped braces
          if((ch == lbr || ch == rbr) && chNext == ch) {
            outBuilder.Append(ch);
            skipNext = true;
            continue;
          }
          // { - start of name
          if(ch == lbr) {
            outBuilder.Append(lbr);
            nameStart = i + 1;
            continue;
          }
          // } - end of name
          if(ch == rbr && nameStart != -1) {
            outBuilder.Append(nameList.Count);
            outBuilder.Append(rbr);
            var name = new string(chars.Skip(nameStart).Take(i - nameStart).ToArray());
            nameList.Add(name);
            nameStart = -1;
            continue; 
          }
          // regular char; if we are not reading name (namestart == -1) then append to outbuilder; otherwise do nothing
          if(nameStart == -1)
            outBuilder.Append(ch);
        }// for i

        adjustedTemplate = outBuilder.ToString();
        argNames = nameList.ToArray();
        //Final test - try to format 
        var test = string.Format(adjustedTemplate, argNames);
        return true; 
      } catch(Exception ex) {
        adjustedTemplate = "(format error: " + ex.Message + "), template: " + template;
        return false; 
      }

    }//method

    public static TEnum ParseEnum<TEnum>(string value, TEnum? defaultValue = null) where TEnum: struct, Enum {
      if (string.IsNullOrWhiteSpace(value)) {
        if(defaultValue != null)
          return defaultValue.Value;
        Util.Throw($"Enum value may not be empty. Enum type: {typeof(TEnum)}.");
      }
      Util.Check(Enum.TryParse<TEnum>(value.Trim(), true, out TEnum ev), "Invalid value: '{0}' for enum {1}.", value, typeof(TEnum));
      return ev;
    }


    public static IList<TEnum> ParseEnumList<TEnum>(string enumValues, char separator = ',') where TEnum: struct, Enum {
      Util.CheckParamNotEmpty(enumValues, nameof(enumValues));
      var strArr = enumValues.Split(separator);
      var result = new List<TEnum>();
      foreach(var s in strArr) {
        var v = ParseEnum<TEnum>(s);
        result.Add(v); 
      }
      return result;
    }

    static MD5 _md5; 
    public static string GetMd5Hash(string value) {
      _md5 ??= MD5.Create();
      value ??= string.Empty;
      var bytes = Encoding.UTF8.GetBytes(value);
      var hash = _md5.ComputeHash(bytes);
      var hexHash = HexUtil.ByteArrayToHex(hash);
      return hexHash; 
    }

  }//class
} //ns
