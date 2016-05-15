using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Login {
  using Api; 

  public interface ILoginAdministrationService {
    ILogin GetLogin(IEntitySession session, Guid loginId);
    SearchResults<ILogin> SearchLogins(OperationContext context, LoginSearch criteria);
    string GenerateTempPassword();
    void SetOneTimePassword(ILogin login, string password);
    void UpdateStatus(ILogin login, bool? disabled, bool? suspended);
  }

}
