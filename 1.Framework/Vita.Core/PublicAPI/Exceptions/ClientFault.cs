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

    public IDictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

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

  /// <summary>Standard code values used in ClientFault.Code field for some common cases.</summary>
  public static class ClientFaultCodes {
    public const string ValueTooLong = "ValueTooLong";
    public const string ValueMissing = "ValueMissing";
    public const string ValueOutOfRange = "ValueOutOfRange";
    public const string InvalidValue = "InvalidValue";
    public const string InvalidAction = "InvalidAction";
    public const string ObjectNotFound = "ObjectNotFound";
    public const string CircularEntityReference = "CircularEntityReference";
    public const string ContentMissing = "ContentMissing"; //missing HTTP message body when some object is expected
    public const string InvalidUrlParameter = "InvalidUrlParameter";
    public const string InvalidUrlOrMethod = "InvalidUrlOrMethod";
    public const string ConcurrentUpdate = "ConcurrentUpdate";
    public const string BadContent = "BadContent"; // failure to deserialize content
    public const string AuthenticationRequired = "AuthenticationRequired"; 
  }

  /// <summary>Thread-safe container for client faults. Exposed in OperationContext.ClientFaults property.</summary>
  public class ClientFaultList  {
    List<ClientFault> _faults = new List<ClientFault>();
    object _lock = new object();
    
    public bool HasFaults() {
      lock(_lock) {
        return _faults.Count > 0;
      }
    }
    public ClientFault this[int index] {
      get {
        lock(_lock) {
          return _faults[index]; 
        }
      }
    }

    public int Count { 
      get { return _faults.Count; } 
    }

    public List<ClientFault> GetAll() {
      lock(_lock) {
        return new List<ClientFault>(_faults);
      }
    }
    public void Add(ClientFault fault) {
      lock(_lock) {
        _faults.Add(fault); 
      }
    }

    public void AddRange(IEnumerable<ClientFault> faults) {
      lock(_lock) {
        _faults.AddRange(faults);
      }
    }
    public void Clear() {
      lock(_lock) {
        _faults.Clear();
      }
    }
    public void Throw() {
      lock(_lock) {
        if(_faults.Count == 0)
          return;
        var cfex = new ClientFaultException(new List<ClientFault>(_faults));
        _faults.Clear();
        throw cfex;
      }
    }//method

  }//class
}//ns
