using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Login {
  using Api; 

  //Supports processes like multi-factor login, password reset, email/phone verification
  public interface ILoginProcessService {
    // Starting the process
    string GenerateProcessToken();
    ILoginExtraFactor FindLoginExtraFactor(OperationContext context, ExtraFactorTypes factorType, string factor); //ex: initial lookup of email
    ILoginExtraFactor FindLoginExtraFactor(ILogin login, string factor); // lookup for known login
    ILoginProcess StartProcess(ILogin login, LoginProcessType processType, string token);
    bool CheckCaptcha(OperationContext context, string captcha);
    string GeneratePin(ILoginProcess process, ILoginExtraFactor factor);
    // One or more cycles of sendpin, submit pin, verify remaining steps, get factor, etc
    // note: In SendPin we provide factor value from user - to avoid decrypting it second time from LoginFactor
    void SendPin(ILoginProcess process, ILoginExtraFactor factor, string factorValue = null, string pin = null); 
    bool SubmitPin(ILoginProcess process, string value); //submit pin entered by user or from clicked URL
    void AbortPasswordReset(ILoginProcess process); //abort - when user clicks abort URL signalling that he did not start the process

    //Note: ProcessType is important, to avoid detecting of existense of a process using different versions GetProcess Api calls!
    ILoginProcess GetActiveProcess(OperationContext context, LoginProcessType processType, string token);
    IList<ILoginProcess> GetActiveConfirmationProcesses(ILoginExtraFactor factor, LoginProcessType processType);
    // Check secret questions answers
    IList<ISecretQuestion> GetUserSecretQuestions(ILogin login); //get user list of questions
    bool CheckSecretQuestionAnswer(ILoginProcess process, ISecretQuestion question, string userAnswer); //submit answer
    bool CheckAllSecretQuestionAnswers(ILoginProcess process, IList<SecretQuestionAnswer> answers);
    //final actions
    void ResetPassword(ILoginProcess process, string newPassword); 
  }

}
