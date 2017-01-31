using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime; 

namespace Vita.Modules.Logging {


  public class IncidentLogModule : EntityModule, IIncidentLogService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    List<IncidentTrigger> _triggers = new List<IncidentTrigger>();

    public IncidentLogModule(EntityArea area, string name = "IncidentLog") : base(area, name, version: CurrentVersion) {
      RegisterEntities(typeof(IIncidentLog), typeof(IIncidentAlert));
      App.RegisterService<IIncidentLogService>(this); 
    }


    #region IIncidentService members
    public IIncidentLog LogIncident(string incidentType, string message, string incidentSubType = null,
                                    Guid? keyId1 = null, Guid? keyId2 = null,
                                    string key1 = null, string key2 = null, string key3 = null, string key4 = null,
                                    string notes = null, OperationContext context = null) {
      if(App.Status != EntityAppStatus.Connected)
        return null; //cannot write log 
      var session = App.OpenSystemSession();
      var log = session.NewEntity<IIncidentLog>();
      log.CreatedOn = App.TimeService.UtcNow;
      log.Type = incidentType;
      log.SubType = incidentSubType;
      log.Message = message;
      log.KeyId1 = keyId1;
      log.KeyId2 = keyId2;
      log.Key1 = key1;
      log.Key2 = key2;
      log.LongKey3 = key3;
      log.LongKey4 = key4; 
      log.Notes = notes;
      //Get web call id if available
      if(context != null) {
        log.UserName = context.User.UserName;
        if (context.UserSession != null)
          log.UserSessionId = context.UserSession.SessionId;
        if (context.WebContext != null)
          log.WebCallId = context.WebContext.Id;
      }
      session.SaveChanges();
      OnNewIncident(log); 
      return log;
    }
    public event EventHandler<IncidentEventArgs> NewIncident;

    public void AddTrigger(IncidentTrigger trigger) {
      Util.Check(trigger != null, "Trigger may not be null.");
      _triggers.Add(trigger); 
    }


    #endregion

    private List<IncidentTrigger> MatchTriggers(string incidentType, string incidentSubType) {
      var matches = _triggers.Where(t => t.IncidentType == incidentType &&
        (t.IncidentSubType == null || t.IncidentSubType == incidentSubType)).ToList();
      return matches; 
    }

    private void OnNewIncident(IIncidentLog log) {
      if(NewIncident != null)
        NewIncident(this, new IncidentEventArgs(log));
      var triggers = MatchTriggers(log.Type, log.SubType);
      foreach(var tr in triggers) {
        tr.OnNewIncident(log);
      }
    }


  }
}
