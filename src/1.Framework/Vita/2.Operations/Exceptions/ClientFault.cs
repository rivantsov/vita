using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities {
  using TDict = Dictionary<string, object>;

  public class ClientFault {
    public string Code;
    public string Message;
    public string Tag;  //optional
    public string EntityRef; // Optional, target object path: 'BookOrder/123'; or for new entities, it can be tempKey assigned by client
    public string PropertyName; 

    public IDictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public ClientFault() { }
    public ClientFault(string code, string message) {
      Code = code;
      Message = message;
    }

    public override string ToString() {
      return Message;
    }
  }
  
}//ns
