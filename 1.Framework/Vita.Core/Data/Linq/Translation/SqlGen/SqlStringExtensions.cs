
using System;
using System.Text;

namespace Vita.Data.Linq.Translation.SqlGen {

    internal static class SqlStringExtensions
    {
        public static string DoubleQuote(this string value) {
          return "\"" + value + "\""; 
        }

        public static string Enquote(this string name, char startQuote, char endQuote)  {
          if (name.Length > 0 && name[0] != startQuote)
            name = startQuote + name;
          if (name.Length > 0 && name[name.Length - 1] != endQuote)
            name = name + endQuote;
          return name;
        }

        public static bool ContainsCase(this string text, string find, bool ignoreCase)
        {
            if (text == null)
                return false;
            var comparison = ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
            var endIndex = text.IndexOf(find, 0, comparison);
            return endIndex >= 0;
        }

        public static string ReplaceCase(this string text, string find, string replace, bool ignoreCase)
        {
            var result = new StringBuilder();
            var comparison = ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
            for (int index = 0; ; )
            {
                var endIndex = text.IndexOf(find, index, comparison);
                if (endIndex >= 0)
                {
                    result.Append(text.Substring(index, endIndex - index));
                    result.Append(replace);
                    index = endIndex + find.Length;
                }
                else
                {
                    result.Append(text.Substring(index));
                    break;
                }
            }
            return result.ToString();
        }
    }
}
