using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Login {

  public class LoginSearch : SearchParams {
    public Guid? UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public DateTime? ExpiringBefore { get; set; }
    public bool EnabledOnly { get; set; }
    public bool SuspendedOnly { get; set; }
  }

  public interface ILoginAdministrationService {
    ILogin GetLogin(IEntitySession session, Guid loginId);
    SearchResults<ILogin> SearchLogins(OperationContext context, LoginSearch criteria);
    string GenerateTempPassword();
    void SetOneTimePassword(ILogin login, string password);
    void UpdateStatus(ILogin login, bool? disabled, bool? suspended);
  }

}
