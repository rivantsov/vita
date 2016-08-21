using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Modules.Notifications; 

namespace Vita.Modules.Login {
  public partial class LoginModule {

    protected virtual void SendPin(ILoginProcess process, ExtraFactorTypes factorType, string factor, string pin) {
      var session = EntityHelper.GetSession(process);
      string mediaType = GetMediaType(factorType);
      Util.CheckNotEmpty(mediaType, "Cannot send pin, unsupported factor type: {0}.", factorType);
      var notificationType = GetPinNotificationType(process.ProcessType);
      var userId = process.Login.UserId;
      var msg = new NotificationMessage() { Type = notificationType, MediaType = mediaType, Recipients = factor, MainRecipientUserId = userId, Culture = session.Context.UserCulture };
      msg.From = _settings.DefaultEmailFrom;
      msg.Parameters[LoginNotificationKeys.BackHitUrlBase] = _settings.BackHitUrlBase; 
      msg.Parameters[LoginNotificationKeys.Pin] = pin;
      msg.Parameters[LoginNotificationKeys.ProcessToken] = process.Token;
      msg.Parameters[LoginNotificationKeys.UserName] = process.Login.UserName;
      _notificationService.Send(session.Context, msg); 
    }

    protected virtual void SendNotification(OperationContext context, string notificationType, string mediaType, string recipient, Guid? userId, IDictionary<string, object> parameters) {
      var msg = new NotificationMessage() { Type = notificationType, MediaType = mediaType, Recipients = recipient, MainRecipientUserId = userId, Culture = context.UserCulture, Parameters = parameters };
      msg.From = _settings.DefaultEmailFrom;
      msg.Parameters[LoginNotificationKeys.BackHitUrlBase] = _settings.BackHitUrlBase;
      _notificationService.Send(context, msg); 
    }

    private string GetPinNotificationType(LoginProcessType processType) {
      switch(processType) {
        case LoginProcessType.FactorVerification: return LoginNotificationTypes.FactorVerifyPin;
        case LoginProcessType.MultiFactorLogin: return LoginNotificationTypes.MultiFactorPin;
        case LoginProcessType.PasswordReset: return LoginNotificationTypes.PasswordResetPin;
        default:
          Util.Throw("Invalid process type {0}, failed to match pin notification type.", processType);
          return null; 
      }
    }

    private string GetMediaType(ExtraFactorTypes factorType) {
      switch (factorType) {
        case ExtraFactorTypes.Email: return NotificationMediaTypes.Email;
        case ExtraFactorTypes.Phone: return NotificationMediaTypes.Sms;
        default: return null;
      }
    }


  }
}
