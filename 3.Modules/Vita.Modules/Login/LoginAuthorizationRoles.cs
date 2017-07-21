using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Authorization;
using Vita.Entities.Model; 

namespace Vita.Modules.Login {
  using Api; 

  public class LoginAuthorizationRoles {

    public readonly Activity LoginEditActivity;
    public readonly Role SelfServiceEditor;  // can edit his/her own login
    public readonly Role LoginAdministrator; //can edit any login
    public readonly ObjectAccessPermission AdministrationControllerPermission;

    internal LoginAuthorizationRoles() {
      AdministrationControllerPermission = new ObjectAccessPermission("LoginAdministrationControllerPermission", AccessType.ApiAll, typeof(LoginAdministrationController));

      // entities we need to protect - only user himself (or superadmin) can edit these: 
      var myLoginData = new EntityGroupResource("LoginEntities", 
                typeof(ILogin), typeof(ILoginExtraFactor), typeof(ITrustedDevice), typeof(ISecretQuestionAnswer), typeof(IPasswordHistory));
      var myLoginDataFilter = new AuthorizationFilter("MyLoginData");
      myLoginDataFilter.Add<ILogin, Guid>((lg, userId) => lg.UserId == userId);
      myLoginDataFilter.Add<ILoginExtraFactor, Guid>((f, userId) => f.Login.UserId == userId);
      myLoginDataFilter.Add<IPasswordHistory, Guid>((ph, userId) => ph.Login.UserId == userId);
      myLoginDataFilter.Add<ITrustedDevice, Guid>((d, userId) => d.Login.UserId == userId);
      myLoginDataFilter.Add<ISecretQuestionAnswer, Guid>((a, userId) => a.Login.UserId == userId);
      var selfServiceEditPermission = new EntityGroupPermission("LoginSelfServiceEdit", AccessType.CRUD, myLoginData);

      LoginEditActivity = new Activity("LoginEdit", selfServiceEditPermission);
      SelfServiceEditor = new Role("LoginSelfServiceEditor");
      SelfServiceEditor.Grant(myLoginDataFilter, LoginEditActivity);
      LoginAdministrator = new Role("LoginAdministrator", LoginEditActivity); // unrestricted edit of any record
      LoginAdministrator.Grant(AdministrationControllerPermission);
      //Api
    }

  }
}
