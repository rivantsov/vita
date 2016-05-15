using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Vita.Common;

namespace Vita.Modules.WebClient {
  public static class WebApiClientUtil {
    public static bool IsSet(this ClientOptions options, ClientOptions option) {
      return (options & option) != 0;
    }
    public static string ToCamelCase(this string value) {
      if (string.IsNullOrEmpty(value))
        return value;
      return Char.ToLowerInvariant(value[0]) + value.Substring(1); 
    }


  }
}
