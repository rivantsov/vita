using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities {

  public enum UserSessionStatus {
    Active = 1,
    Expired = 2,
    LoggedOut = 3,
    PendingMultifactor = 4,
  }


  public class UserSessionBase {
    public Guid SessionId;
    public UserSessionStatus Status; 

  }
}
