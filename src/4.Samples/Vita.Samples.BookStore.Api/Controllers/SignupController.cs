using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Vita.Entities;
using Vita.Entities.Api;
using Vita.Modules.Login;
using Vita.Web;

namespace Vita.Samples.BookStore.Api {

  /// <summary> Signup functionality - creating user accounts for web visitors (customers)
  /// </summary>
  [Route("api/signup")]
  public class SignupController : BaseApiController {

    [HttpPost]
    public User SignupUser([FromBody] UserSignup signup) {
      // signup request contains password, so mark it confidential, so the request body will NOT be logged
      //  to prevent passwords appearing in logs
      this.WebContext.Flags |= WebCallFlags.Confidential; 

      //Validate 
      OpContext.ThrowIfNull(signup, ClientFaultCodes.InvalidValue, "UserSignup", "UserSignup object may not be null.");
      OpContext.ValidateNotEmpty(signup.UserName, "UserName", "UserName may not be empty.");
      OpContext.ValidateNotEmpty(signup.Password, "Password", "Password may not be empty.");
      OpContext.ThrowValidation(); 
      var session = OpenSession();
      // check if user name is already taken
      var existingUser = session.EntitySet<IUser>().Where(u => u.UserName == signup.UserName).WithOptions(QueryOptions.ForceIgnoreCase).FirstOrDefault();
      OpContext.ThrowIf(existingUser != null, ClientFaultCodes.InvalidValue, "UserName", "User name {0} is already in use. Please choose other name.", signup.UserName);
      // create login and user
      var loginMgr = OpContext.App.GetService<ILoginManagementService>();
      var user = session.NewUser(signup.UserName, UserType.Customer, signup.UserName);
      var login = loginMgr.NewLogin(session, signup.UserName, signup.Password, loginId: user.Id, userId: user.Id); //Login.Id is the same as userID
      session.SaveChanges();
      // Let's create custom app event; we test here that this appEvent gets to AppEventLog
      //  log entries in web app are accumulated in buffered log attached to web call, and later 
      // dumped to WebCallLog.OperationLog column as text. Except - errors and app events, they are 
      // passed to global log as well. 
      OpContext.WriteAppEvent("Customer", "Signup", $"Customer {signup.UserName} signed up.");
      return user.ToModel(); 
    }

  }
}
