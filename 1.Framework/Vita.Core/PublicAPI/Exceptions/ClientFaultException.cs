using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using Vita.Entities.Runtime; 
using Vita.Common; 

namespace Vita.Entities {

  public class ClientFaultException : OperationAbortException {
    /// <summary>If set to true, suppresses listing of fault messages in Message property. By default is false. </summary>
    public static bool SuppressFaultsListInMessage; 
    public readonly IList<ClientFault> Faults;

    public ClientFaultException(IList<ClientFault> faults, string message = "Client faults detected.", Exception inner = null) 
                        : base(FormatMessage(message, faults), OperationAbortReasons.ClientFault, inner) {
      Faults = faults; 
    }

    public ClientFaultException(string code, string message, string tag = null, string path = null, Exception inner = null)
      : base(message, OperationAbortReasons.ClientFault, inner) {
      Faults = new List<ClientFault>();
      Faults.Add(new ClientFault(code, message, tag, path));
    }

    public string[] GetMessages() {
      return Faults.Select(s => s.Message).ToArray();
    }
    public static  string FormatMessage(string message, IList<ClientFault> faults) {
      if(SuppressFaultsListInMessage)
        return message; 
      var msg = message + " " + string.Join(" ", faults.Select(s => s.Message));
      return msg; 
    }

    public override string ToString() {
      var baseStr = base.ToString(); 
      var nl = Environment.NewLine;
      return baseStr + nl  + "Faults:" + nl + string.Join(nl + "  ", GetMessages()) + nl;
    }

  }//class
}//namespace
