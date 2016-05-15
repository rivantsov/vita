using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Logging {
  
  public class IncidentEventArgs : EventArgs {
    public readonly IIncidentLog Incident;
    public IncidentEventArgs(IIncidentLog incident) {
      Incident = incident; 
    }
  }
  public class IncidentAlertEventArgs : EventArgs {
    public readonly IIncidentAlert Alert;
    public IncidentAlertEventArgs(IIncidentAlert alert) {
      Alert = alert; 
    }
  }

  public interface IIncidentLogService {
    IIncidentLog LogIncident(string incidentType, string message, string incidentSubType = null,                                     
                                    Guid? keyId1 = null, Guid? keyId2 = null,
                                    string key1 = null, string key2 = null, string key3 = null, string key4 = null,
                                    string notes = null, OperationContext operationContext = null);
    event EventHandler<IncidentEventArgs> NewIncident;
    void AddTrigger(IncidentTrigger trigger);
  }

}
