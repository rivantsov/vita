using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Modules.Logging {

  public class IncidentTrigger {
    public readonly string IncidentType;
    public readonly string IncidentSubType;
    public IncidentTrigger(string incidentType, string incidentSubType = null) {
      Util.Check(!string.IsNullOrWhiteSpace(incidentType), "IncidentType may not be empty.");
      IncidentType = incidentType;
      IncidentSubType = incidentSubType;
    }
    
    public virtual void OnNewIncident(IIncidentLog newEntry) {
    }
  }

}
