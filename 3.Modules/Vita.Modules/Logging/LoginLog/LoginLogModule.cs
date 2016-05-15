using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Modules.Login;

namespace Vita.Modules.Logging {


  public class LoginLogModule : EntityModule, ILoginLogService, IObjectSaveHandler {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    IBackgroundSaveService _backgroundSave;


    public LoginLogModule(EntityArea area, string name = "LoginLog", string description = null) : base(area, name, description, version: CurrentVersion) {
      RegisterEntities(typeof(ILoginLog)); 
      App.RegisterService<ILoginLogService>(this); 
    }

    public override void Init() {
      base.Init();
      _backgroundSave = App.GetService<IBackgroundSaveService>();
      _backgroundSave.RegisterObjectHandler(typeof(LoginLogEntry), this);
    }

    #region LoginLogService implementation

    public void LogEvent(OperationContext context, LoginEventType eventType, ILogin login = null, string notes = null, string userName = null) {
      var webCtx = context.WebContext;
      Guid? loginId = null;
      if(login != null) {
        loginId = login.Id;
        if(userName == null)
          userName = login.UserName;
      }
      var logEntry = new LoginLogEntry(context, loginId, eventType.ToString(), notes, userName);
      _backgroundSave.AddObject(logEntry);
    }

    public ILoginLog GetLastEvent(Guid loginId, LoginEventType eventType) {
      var session = App.OpenSystemSession();
      var evt = session.EntitySet<ILoginLog>().Where(l => l.LoginId == loginId && l.EventType == eventType.ToString()).OrderByDescending(l => l.CreatedOn).FirstOrDefault();
      return evt; 
    }

    #endregion

    #region IObjectSaveHandler members
    void IObjectSaveHandler.SaveObjects(IEntitySession session, IList<object> items) {
      foreach (LoginLogEntry e in items) {
        var entry = session.NewEntity<ILoginLog>();
        entry.CreatedOn = e.CreatedOn;
        entry.UserName = e.UserName;
        entry.LoginId = e.LoginId;
        entry.EventType = e.EventType;
        entry.Notes = e.Notes;
        entry.WebCallId = e.WebCallId;
      }
    }
    #endregion

  }
}
