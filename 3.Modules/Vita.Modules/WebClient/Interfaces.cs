using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.WebClient {

  public interface IContentSerializer {
    IList<string> MediaTypes { get; }
    Task<object> DeserializeAsync(Type type, HttpContent content);
    StreamContent Serialize(object value);
  }

  public interface IClientErrorHandler {
    Task<Exception> HandleErrorAsync(HttpResponseMessage response); 

  }
}
