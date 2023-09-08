using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Vita.Entities {

  public class ClientFaultException : OperationAbortException {
    /// <summary>If set to true, suppresses listing of fault messages in Message property. By default is false. </summary>
    public static bool SuppressFaultsListInMessage; 
    public readonly List<ClientFault> Faults = new List<ClientFault>();

    public ClientFaultException(string message, Exception inner = null)
                         : base(message, OperationAbortReasons.ClientFault, inner) {
    }
    public ClientFaultException(ClientFault fault, string message, Exception inner = null)
                         : this(message, inner) {
      Faults.Add(fault);
    }


    public ClientFaultException(IList<ClientFault> faults, string message = "Client faults detected.", Exception inner = null) 
                        : this(AppendFaults(message, faults)) {
      Faults.AddRange(faults); 
    }

    public string[] GetMessages() {
      return Faults.Select(s => s.Message).ToArray();
    }
    public static  string AppendFaults(string message, IList<ClientFault> faults) {
      if(SuppressFaultsListInMessage || faults == null)
        return message;
      var nl = Environment.NewLine;
      var msg = message + nl + string.Join(nl, faults.Select(s => s.Message));
      return msg; 
    }

    public override string ToString() {
      var baseStr = base.ToString(); 
      var nl = Environment.NewLine;
      return baseStr + nl  + "Faults:" + nl + string.Join(nl + "  ", GetMessages()) + nl;
    }

  }//class
}//namespace
