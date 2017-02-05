using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.WebClient {

  public class SerializedContent {
    public object Object;
    public HttpContent Content;
    public string Raw;
    public override string ToString() {
      return Raw ?? Object?.ToString(); 
    }
  }

  public interface IContentSerializer {
    IList<string> MediaTypes { get; }
    Task<SerializedContent> DeserializeAsync(Type type, HttpContent content);
    Task<SerializedContent> SerializeAsync(object value);
  }

}
