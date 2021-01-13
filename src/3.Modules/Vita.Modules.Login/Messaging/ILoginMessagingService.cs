using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.Login {

  public enum LoginMessageType {
    Pin,
    PasswordResetCompleted,
  }

  public interface ILoginMessagingService {
    Task SendMessage(OperationContext context, LoginMessageType messageType, ILoginExtraFactor factor, ILoginProcess process = null);
  }


}
