using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities {

  public class ClientFault {
    public string Code;
    public string Message;
    public string Tag;  //Property name, optional
    public string Path; // Optional, target object path: 'BookOrder/123'

    public IDictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public ClientFault() { }
    public ClientFault(string code, string message, string tag = null, string path = null) {
      Code = code;
      Message = message;
      Tag = tag;
      Path = path; 
    }

    public override string ToString() {
      return Message;
    }
  }



}//ns
