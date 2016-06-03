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

  public class JsonDeserializerAdapter : IJsonDeserializer {
    JsonSerializer _serializer = new Newtonsoft.Json.JsonSerializer();

    public JsonDeserializerAdapter(JsonSerializer serializer = null) {
      _serializer = serializer ?? new JsonSerializer(); 
    }

    public T Deserialize<T>(string json) {
      var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
      using(JsonTextReader jsonTextReader = new JsonTextReader(new StreamReader(stream, Encoding.UTF8))) {
        return _serializer.Deserialize<T>(jsonTextReader);
      }
    }

  }//class


}
