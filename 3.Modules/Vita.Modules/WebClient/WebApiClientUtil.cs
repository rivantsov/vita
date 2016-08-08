using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Vita.Common;
using Vita.Modules.WebClient.Json;

namespace Vita.Modules.WebClient {
  public static class WebApiClientUtil {
    public static bool IsSet(this ClientOptions options, ClientOptions option) {
      return (options & option) != 0;
    }
    public static Newtonsoft.Json.JsonSerializer GetJsonSerializer(this WebApiClient client) {
      var jsonContentSer = (JsonContentSerializer) client.Settings.ContentSerializer;
      if(jsonContentSer == null)
        return null;
      return jsonContentSer.JsonSerializer;
    }


  }
}
