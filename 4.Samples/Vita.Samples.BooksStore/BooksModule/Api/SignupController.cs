using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Web;
using Vita.Modules.Login;

namespace Vita.Samples.BookStore.Api {

  /// <summary> Signup functionality - creating user accounts for web visitors (customers)
  /// </summary>
  [ApiRoutePrefix("signup")]
  class SignupController : SlimApiController {

    protected IEntitySession OpenSession() {
      return Context.OpenSession();
    }

    [ApiPost]
    public User SignupUser(UserSignup signup) {
      //Validate 
      Context.ThrowIfNull(signup, ClientFaultCodes.InvalidValue, "UserSignup", "UserSignup object may not be null.");
      Context.ValidateNotEmpty(signup.UserName, "UserName", "UserName may not be empty.");
      Context.ValidateNotEmpty(signup.Password, "Password", "Password may not be empty.");
      Context.ThrowValidation(); 
      var session = OpenSession();
      // check if user name is already taken
      var existingUser = session.EntitySet<IUser>().Where(u => u.UserName == signup.UserName).WithOptions(QueryOptions.ForceIgnoreCase).FirstOrDefault();
      Context.ThrowIf(existingUser != null, ClientFaultCodes.InvalidValue, "UserName", "User name {0} is already in use. Please choose other name.", signup.UserName);
      // create login and user
      var loginMgr = Context.App.GetService<ILoginManagementService>();
      var user = session.NewUser(signup.UserName, UserType.Customer, signup.UserName);
      var login = loginMgr.NewLogin(session, signup.UserName, signup.Password, loginId: user.Id, userId: user.Id); //Login.Id is the same as userID
      session.SaveChanges(); 
      return user.ToModel(); 
    }

  }
}
