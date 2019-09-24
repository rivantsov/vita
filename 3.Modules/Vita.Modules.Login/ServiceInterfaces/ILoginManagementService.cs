using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities;

namespace Vita.Modules.Login {

  public interface ILoginManagementService {
    ILogin NewLogin(IEntitySession session, string userName, string password,
        DateTime? expires = null, LoginFlags flags = LoginFlags.None, Guid? loginId = null, 
        Guid? userId = null, Int64? altUserId = null, Guid? tenantId = null);

    ILogin GetLogin(IEntitySession session);
    void UpdateLogin(ILogin login, LoginInfo loginInfo);
    IList<ISecretQuestion> GetAllSecretQuestions(IEntitySession session);
    IList<ISecretQuestion> GetUserSecretQuestions(ILogin login);
    void UpdateUserQuestionAnswers(ILogin login, IList<SecretQuestionAnswer> answers);
    void ReorderUserQuestionAnswers(ILogin login, IList<Guid> ids);
    ISecretQuestionAnswer AddSecretQuestionAnswer(ILogin login, int number, ISecretQuestion question, string answer); 

    IList<LoginExtraFactor> GetUserFactors(ILogin login);
    LoginExtraFactor GetUserFactor(ILogin login, Guid factorId);
    LoginExtraFactor AddFactor(ILogin login, ExtraFactorTypes type, string value);
    LoginExtraFactor UpdateFactor(ILoginExtraFactor factor, string value);
    string GetGoogleAuthenticatorQRUrl(ILoginExtraFactor factor);
    bool CheckLoginFactorsSetupCompleted(ILogin login);

    ITrustedDevice RegisterTrustedDevice(ILogin login, DeviceType type, DeviceTrustLevel trustLevel);
    bool RemoveTrustedDevice(ILogin login, string deviceToken);

    void ChangePassword(ILogin login, string oldPassword, string password);
    PasswordStrength EvaluatePasswordStrength(string password);

  }

}
