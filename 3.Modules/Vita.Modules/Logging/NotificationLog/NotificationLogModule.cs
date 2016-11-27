using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services;
using Vita.Modules.Notifications;

namespace Vita.Modules.Logging {
  public class NotificationLogModule : EntityModule, INotificationLogService, IObjectSaveHandler {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    
    IBackgroundSaveService _backgroundSave;

    public NotificationLogModule(EntityArea area) : base(area, "NotificationLog", version: CurrentVersion) {
      RegisterEntities(typeof(INotificationLog));
      App.RegisterService<INotificationLogService>(this); 
    }

    public override void Init() {
      base.Init();
      _backgroundSave = App.GetService<IBackgroundSaveService>();
      _backgroundSave.RegisterObjectHandler(typeof(NotificationLogEntry), this);
    }

    public Guid LogMessage(OperationContext context, NotificationMessage message) {
      var entry = new NotificationLogEntry(context, message);
      _backgroundSave.AddObject(entry);
      return entry.Id.Value; //it is always there
    }

    public void SaveObjects(Entities.IEntitySession session, IList<object> items) {
      foreach (NotificationLogEntry entry in items) {
        var ent = session.NewLogEntity<INotificationLog>(entry);
        var msg = entry.Message; 
        ent.Type = msg.Type;
        ent.MediaType = msg.MediaType;
        ent.Body = msg.Body; 
        ent.Error = msg.Error;
        ent.AttemptCount = msg.AttemptCount;
        ent.Status = msg.Status.ToString(); 
        ent.MainRecipientUserId = msg.MainRecipientUserId;
        ent.Recipients = msg.Recipients; 
        var recipients = entry.Message.Recipients.Split(';');
        switch (recipients.Length) {
          case 0:
            ent.MainRecipient = "(none)";
            break;
          case 1:
            ent.MainRecipient = recipients[0];
            break;
          default:
            ent.MainRecipient = recipients[0];
            break;
        }
        if (entry.Message.Attachments.Count > 0)
          ent.AttachmentList = string.Join(", ", entry.Message.Attachments.Select(a => a.ToString()));
        //simplified for now
        ent.Parameters = string.Join("|||", msg.Parameters.Select(kv => kv.Key + "=" + kv.Value)); 
      }//foreach
    }//method


  }
}
