using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Modules.Login {

  public class LoginResult {
    public LoginAttemptStatus Status;
    public ILogin Login;
    public PostLoginActions Actions;
    public Guid SessionId;
    public UserInfo User;
    public DateTime? PreviousLoginOn;
  }

  public class LoginEventArgs : EventArgs {
    public readonly LoginEventType EventType;
    public readonly ILogin Login;
    public readonly OperationContext Context;
    public LoginEventArgs(LoginEventType eventType, ILogin login, OperationContext context) {
      EventType = eventType;
      Login = login;
      Context = context; 
    }
  }

  //Low-level login service
  public interface ILoginService {
    //event
    event EventHandler<LoginEventArgs> LoginEvent;
    //high-level methods
    LoginResult Login(OperationContext context, string userName, string password, Guid? tenantId = null, string deviceToken = null);
    LoginResult CompleteMultiFactorLogin(OperationContext context, ILogin login);
    void Logout(OperationContext context);

    //Low-level methods
    ILogin FindLogin(IEntitySession session, string userName, string password, Guid? tenantId);
    //Device token identifies trusted device
    LoginAttemptStatus CheckCanLogin(ILogin login, string deviceToken = null);
    PostLoginActions GetPostLoginActions(ILogin login);

    // used by OAuth
    LoginResult LoginUser(OperationContext context, Guid userId);
  }




}
