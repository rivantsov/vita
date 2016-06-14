using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using System.IO;

namespace Vita.Modules.OAuthClient {

  public interface IJsonDeserializer {
    T Deserialize<T>(string json);
  }

  public class JsonDeserializer : IJsonDeserializer {
    JsonSerializer _serializer;

    public JsonDeserializer(JsonSerializer serializer = null) {
      _serializer = serializer;
      if(_serializer == null) {
        _serializer = new Newtonsoft.Json.JsonSerializer();
        // to support Node() attr
        _serializer.ContractResolver = new WebClient.Json.NodeNameContractResolver(WebClient.ClientOptions.Default);
      }
    }

    public T Deserialize<T>(string json) {
      var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
      using(JsonTextReader jsonTextReader = new JsonTextReader(new StreamReader(stream, Encoding.UTF8))) {
        return _serializer.Deserialize<T>(jsonTextReader);
      }
    }

  }//class


}
