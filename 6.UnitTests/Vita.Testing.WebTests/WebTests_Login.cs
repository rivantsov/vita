using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Diagnostics;

using Vita.Web;
using Vita.Samples.BookStore;
using Vita.Entities;
using Vita.Modules.Login;
using Vita.Samples.BookStore.Api;
using Vita.Tools.Testing;
using Vita.NRestClient;

namespace Vita.UnitTests.Web {

  public partial class WebTests {

    [TestMethod]
    public void TestLogin() {
      var client = TestStartup.Client;
      var loginUrl = "api/login";

      // try bad credentials
      var resp = LoginAs("foo", "bar", assertSuccess: false);
      Assert.IsTrue(resp != null && resp.Status == LoginAttemptStatus.Failed, "Login should fail");
      //Login as Dora
      resp = LoginAs("dora");
      //Logout
      client.ExecuteDelete(loginUrl);
      client.RemoveRequestHeader("Authorization");

      // Bug fix: double-login without logout
      var dora2 = LoginAs("dora");
      var diego2 = LoginAs("diego");
      Assert.AreNotEqual(dora2.AuthenticationToken, diego2.AuthenticationToken, "Expected different auth token.");
    }

    [TestMethod]
    public void TestSignup() {
      var client = TestStartup.Client;
      //bad signup attempts
      var badSignup = new UserSignup() { UserName = "dora", Password = "abcefg", DisplayName = "Anonymous" }; //should get 'username is already used'
      var faultExc = TestUtil.ExpectClientFault(() => client.ExecutePost<UserSignup, User>(badSignup, "api/signup"));
      Assert.IsTrue(faultExc.Faults[0].Message.Contains("already in use"), "Expected 'username already in use' error");
      // fix user name, now should get weak password
      badSignup.UserName = "somebody"; 
      faultExc = TestUtil.ExpectClientFault(() => client.ExecutePost<UserSignup, User>(badSignup, "api/signup"));
      Assert.IsTrue(faultExc.Faults[0].Message.Contains("strength criteria"), "Expected 'weak password' error");

      // good signup
      var spikeSignup = new UserSignup() { UserName = "spike", Password = "spikePass123!", DisplayName = "SpikeTheDog" };
      var spike = client.ExecutePost<UserSignup, User>(spikeSignup, "api/signup");
      Assert.IsNotNull(spike, "Expected Spike user object.");
      //try to login
      LoginAs(spikeSignup.UserName, spikeSignup.Password);
      Logout();

    }
    [TestMethod]
    public void TestLoginAdministration() {
      var client = TestStartup.Client;

      // Test authorization - access to login admin API controller is allowed only to administrators
      // Dora is not admin
      LoginAs("dora");
      var exc = TestUtil.ExpectFailWith<RestException>(() => client.ExecuteGet<SearchResults<LoginInfo>>("api/logins?take=1"));
      Assert.AreEqual(HttpStatusCode.Forbidden, exc.Status, "Expected Forbidden");
      Logout();


      // Kevin is store admin
      LoginAs("kevin"); 

      // Test login search
      // No criteria, just skip/take
      var logins = client.ExecuteGet<SearchResults<LoginInfo>>("api/logins?skip=1&take=2");
      Assert.AreEqual(2, logins.Results.Count, "exected 2 logins.");

      // find by user name (partial) and email
      var cartmanEmail = "cartman@email.com";
      logins = client.ExecuteGet<SearchResults<LoginInfo>>("api/logins?username={0}&email={1}", "car", cartmanEmail);
      Assert.AreEqual(1, logins.Results.Count, "Expected to find cartman");

      // search using multiple criteria
      var utcNow = DateTime.UtcNow;
      logins = client.ExecuteGet<SearchResults<LoginInfo>>("api/logins?" +
        "username={0}&email={1}&createdafter={2}&expiringbefore={3}&enabledonly={4}",
            "d", "dduck@warner.com", utcNow.AddMonths(-1), utcNow.AddMonths(1), true);
      //it will return 0 records, we just check it builds correct SQL and does not blow up
      Assert.AreEqual(0, logins.Results.Count, "Expected to find dufffy");

      // Set one time password; find Cartman and set his password
      logins = client.ExecuteGet<SearchResults<LoginInfo>>("api/logins?username={0}", "cartm");
      Assert.AreEqual(1, logins.Results.Count, "Expected cartman's login");
      var cartmanId = logins.Results[0].Id;
      var tempPwd = client.ExecutePut<object, OneTimePasswordInfo>(null, "api/logins/{0}/temppassword", cartmanId);
      Assert.IsNotNull(tempPwd, "Expected password");
      Assert.IsFalse(string.IsNullOrWhiteSpace(tempPwd.Password), "Expected non-empty one-time password");
      Assert.IsTrue(tempPwd.ExpiresHours > 0, "Expected expiration hours > 0.");

      // Let's check if temp pwd works
      Logout();
      var loginResp = LoginAs("cartman", tempPwd.Password);
      Assert.AreEqual(LoginAttemptStatus.Success, loginResp.Status, "Failed to login with one-time password");
      Assert.IsTrue(loginResp.Actions.IsSet(PostLoginActions.ForceChangePassword), "Expected Force change password action");
      Logout();

      // let's try to login one more time - it should fail now, it was one-time password
      loginResp = LoginAs("cartman", tempPwd.Password, assertSuccess: false);
      Assert.AreNotEqual(LoginAttemptStatus.Success, loginResp.Status, "Expected to fail to login twice with one-time password");

      //just to make sure we're logged out
      Logout();
    }

    [TestMethod]
    public void TestLoginMultipleFailures() {
      var client = TestStartup.Client;
      //first try successful login
      var resp = LoginAs("stan");
      Logout();
      var stanLoginId = resp.LoginId;

      // try bad password 5 times
      for (int i = 0; i < 5; i++) {
        resp = LoginAs("stan", "bad-pass", assertSuccess: false);
        Assert.AreEqual(LoginAttemptStatus.Failed, resp.Status, "expected login to fail");
      }
      // Now account should be suspended
      resp = LoginAs("stan", assertSuccess: false);
      // System would respond LoginFailed (NOT AccountInactive) to avoid disclosing the membership
      Assert.AreEqual(LoginAttemptStatus.Failed, resp.Status, "Expected account suspended and login fail.");

      //Admin can enable it back
      LoginAs("kevin");
      //get login info for stan
      var stanLoginInfo = client.ExecuteGet<LoginInfo>("api/logins/{0}", stanLoginId);
      Assert.IsNotNull(stanLoginInfo, "Failed to get stan's login info");
      Assert.IsTrue(stanLoginInfo.Flags.IsSet(LoginFlags.Suspended), "Expected suspended status");
      //Reactivate
      client.ExecutePut<object, HttpStatusCode>(null, "api/logins/{0}/status?suspend=false", stanLoginId);
      stanLoginInfo = client.ExecuteGet<LoginInfo>("api/logins/{0}", stanLoginId);
      Assert.IsFalse(stanLoginInfo.Flags.IsSet(LoginFlags.Disabled | LoginFlags.Suspended), "Expected active login");
      Logout();
      resp = LoginAs("stan", assertSuccess: false);
      Assert.AreEqual(LoginAttemptStatus.Success, resp.Status, "Expected account re-enabled.");


    }

    // Demos: setting up secret questions; verifying email; password reset, multifactor login
    [TestMethod]
    public void TestLoginAdvancedFeatures() {
      // ================================ Completing login setup - verifying email, setup secret questions =====================
      // User Ferb has email (non-verified) and no secret questions setup. Let's setup his account thru API calls.
      var client = TestStartup.Client;
      var ferbUserName = "Ferb";
      var ferbEmail = "ferb@email.com";

      var ferbLogin = LoginAs(ferbUserName);
      Assert.AreEqual(LoginAttemptStatus.Success, ferbLogin.Status, "Ferb login failed.");
      Assert.IsTrue(ferbLogin.Actions.IsSet(PostLoginActions.SetupExtraFactors), "Expected request to setup extra factors.");

      // Get login info, check which factors we need to setup
      var loginInfo = client.ExecuteGet<LoginInfo>("api/mylogin");
      Assert.IsNotNull(loginInfo, "Failed to get LoginInfo");
      Assert.AreEqual(ExtraFactorTypes.Email | ExtraFactorTypes.SecretQuestions, loginInfo.IncompleteFactors, "Wrong set of incomplete factors.");
      //We need to setup email; email might exist or not, and if exists, it might be not verified. 
      var factors = client.ExecuteGet<List<LoginExtraFactor>>("api/mylogin/factors");
      Assert.AreEqual(0, factors.Count, "expected 0 factors");
      var newEmail = new LoginExtraFactor() { Type = ExtraFactorTypes.Email, Value = ferbEmail };
      client.ExecutePost<LoginExtraFactor, LoginExtraFactor>(newEmail, "api/mylogin/factors");

      //Get factors again - now we have email, unconfirmed
      factors = client.ExecuteGet<List<LoginExtraFactor>>("api/mylogin/factors");
      var emailFactor = factors[0];
      Assert.AreEqual(ExtraFactorTypes.Email, emailFactor.Type, "Expected email");
      Assert.IsFalse(emailFactor.Confirmed, "Email should not be confirmed.");
      //Let's confirm it - send pin, read it from email and confirm it
      // The call returns processToken identifying email verificaiton process
      var processTokenBox = client.ExecutePost<object, BoxedValue<string>>(null, "api/mylogin/factors/{0}/pin", emailFactor.Id);
      //let's do it twice - to make sure it works even if we have multiple pins sent
      processTokenBox = client.ExecutePost<object, BoxedValue<string>>(null, "api/mylogin/factors/{0}/pin", emailFactor.Id);
      var pinEmail = TestStartup.GetLastMessageTo(ferbEmail);
      Assert.IsNotNull(pinEmail, "Pin email not received.");
      var pin = pinEmail.Pin; //get pin from email
      Assert.IsFalse(string.IsNullOrEmpty(pin), "Expected non-null pin");
      //submit pin
      // method 1 - endpoint for logged in user, used when user copy/pastes pin on a page
      // var pinOk = client.ExecutePut<object, bool>(null, "api/mylogin/factors/{0}/pin/{1}", emailFactor.Id, pin);
      // method 2 - endpoint not requiring logged-in user; use it in a page activated from URL embedded in email: 
      var pinOk = client.ExecutePut<object, bool>(null, "api/login/factors/verify-pin?processtoken={0}&&pin={1}",
            processTokenBox.Value, pin);
      Assert.IsTrue(pinOk, "Pin submit failed.");
      //Now email should not be listed as incomplete factor
      loginInfo = client.ExecuteGet<LoginInfo>("api/mylogin");
      Assert.AreEqual(ExtraFactorTypes.SecretQuestions, loginInfo.IncompleteFactors, "Expected only questions as incomplete factors.");
      //Let's setup secret questions/answers. Let's get all secret questions, choose three and submit answers
      var allQuestions = client.ExecuteGet<List<SecretQuestion>>("api/mylogin/allquestions");
      Assert.IsTrue(allQuestions.Count > 20, "Failed to retrieve all questions.");
      // let's choose 3
      var qFriend = allQuestions.First(q => q.Question.Contains("friend")); // childhood friend
      var qFood = allQuestions.First(q => q.Question.Contains("favorite food"));
      var qColor = allQuestions.First(q => q.Question.Contains("favorite color"));
      var answers = new SecretQuestionAnswer[] {
        new SecretQuestionAnswer() {QuestionId = qFriend.Id, Answer = "Phineas"},
        new SecretQuestionAnswer() {QuestionId = qFood.Id, Answer = "Potato"},
        new SecretQuestionAnswer() {QuestionId = qColor.Id, Answer = "Blue"}
      };
      //submit answers
      client.ExecutePut<SecretQuestionAnswer[], HttpStatusCode>(answers, "api/mylogin/answers");
      // Read back LoginInfo - now it should have no incomplete factors
      loginInfo = client.ExecuteGet<LoginInfo>("api/mylogin");
      Assert.AreEqual(ExtraFactorTypes.None, loginInfo.IncompleteFactors, "Expected no incomplete factors.");
      //Now if Ferb logs in again, no post-login actions should be required
      Logout();
      ferbLogin = LoginAs(ferbUserName);
      Assert.AreEqual(LoginAttemptStatus.Success, ferbLogin.Status, "Ferb login failed.");
      Assert.AreEqual(PostLoginActions.None, ferbLogin.Actions, "Expected no post-login actions.");

      //============================== Password change =============================================
      // Ferb changes his password; let's first try invalid old password
      var oldPassword = Samples.BookStore.SampleData.SampleDataGenerator.DefaultPassword;
      var newPass = oldPassword + "New";
      var pwdChange = new PasswordChangeInfo() { OldPassword = "bad-old-pass", NewPassword = newPass };
      var cfExc = TestUtil.ExpectClientFault(() => client.ExecutePut<PasswordChangeInfo, HttpStatusCode>(pwdChange, "api/mylogin/password"));
      Assert.AreEqual(ClientFaultCodes.InvalidValue, cfExc.Faults[0].Code, "Expected 'InvalidValue' for OldPassword");
      Assert.AreEqual("OldPassword", cfExc.Faults[0].Tag, "Expected OldPassword as invalid value");
      // Let's try weak password and check it
      pwdChange = new PasswordChangeInfo() { OldPassword = oldPassword, NewPassword = "weakpass" };
      var strength = client.ExecutePut<PasswordChangeInfo, PasswordStrength>(pwdChange, "api/login/passwordcheck");
      Assert.AreEqual(PasswordStrength.Weak, strength, "Expected Weak or unacceptable result.");
      // We set min strength to Medium in login settings
      // let's try weak password - should fail with 'WeakPassword' fault;
      cfExc = TestUtil.ExpectClientFault(() => client.ExecutePut<PasswordChangeInfo, HttpStatusCode>(pwdChange, "api/mylogin/password"));
      Assert.AreEqual(LoginFaultCodes.WeakPassword, cfExc.Faults[0].Code, "Expected WeakPassword fault.");
      // good password
      pwdChange.NewPassword = newPass;
      // check strength
      strength = client.ExecutePut<PasswordChangeInfo, PasswordStrength>(pwdChange, "api/login/passwordcheck");
      Assert.AreEqual(PasswordStrength.Strong, strength, "Expected Strong result.");
      // actually change
      var status = client.ExecutePut<PasswordChangeInfo, HttpStatusCode>(pwdChange, "api/mylogin/password");
      Assert.AreEqual(HttpStatusCode.OK, status, "Password change failed");
      //verify it
      Logout();
      ferbLogin = LoginAs(ferbUserName, pwdChange.NewPassword);
      Assert.AreEqual(LoginAttemptStatus.Success, ferbLogin.Status, "Failed to login with new password.");
      Logout();

      //========================================== Password reset =============================================================
      // Ferb has everything setup for proper password reset (email is comfirmed and secret questions are entered).
      // Lets do password reset. Ferb forgets his password and comes back to our site
      // 1. Start password reset process.  Ferb clicks Forgot Password link and is redirected to initial reset page.
      //  He enters email in a text box, solves captcha and clicks Submit. The client code executes a request to start reset process
      //  "Magic" is a magic captcha value (it is set in login module settings) to bypass captcha check in unit tests.
      var request = new PasswordResetStartRequest() { Factor = ferbEmail, Captcha = "Magic" };
      processTokenBox = client.ExecutePost<PasswordResetStartRequest, BoxedValue<string>>(request, "api/passwordreset/start");
      var processToken = processTokenBox.Value;
      Assert.IsFalse(string.IsNullOrWhiteSpace(processToken), "Expected process token.");
      // We do not disclose any details, even the fact that actual process started or not;
      // even if ferb's email is not found, the server returns a process token as if everything is ok. 
      // This is done to avoid disclosing if the user is signed up at our site or not (if we are a porn site we should not disclose membership)
      // Client can use process token to retrieve LoginProcess object - except right after process is started, it returns null - to avoid disclosing membership.
      // Only after at least one factor (email or phone) is confirmed (pin submitted back), the process information becomes visible. 
      // So at this point trying to get process returns null
      // NOTE: in some cases hiding membership is not needed - when we have a business system with employees as users.
      // For this case, you can set a flag DoNotConcealMembership in ILogin record(s), and system would behave accordingly 
      var process = client.ExecuteGet<LoginProcess>("api/passwordreset/process?token={0}", processToken);
      Assert.IsNull(process, "Expected server hiding process object");
      // 2. Send pin using email 
      var sendPinRequest = new SendPinRequest() { ProcessToken = processToken, FactorType = ExtraFactorTypes.Email, Factor = ferbEmail };
      var httpStatus = client.ExecutePost<SendPinRequest, HttpStatusCode>(sendPinRequest, "api/passwordreset/pin/send");
      Assert.AreEqual(HttpStatusCode.OK, httpStatus, "Failed to send pin.");
      // 3. Ferb receives email - we check our mock email service, retrieve the message and pin
      pinEmail = TestStartup.GetLastMessageTo(ferbEmail);
      Assert.IsNotNull(pinEmail, "Email with pin not received.");
      pin = pinEmail.Pin;
      Assert.IsTrue(!string.IsNullOrWhiteSpace(pin), "Failed to receive/extract pin.");
      // 4. Ferb copies pin from email and enters it in a page. The UI submits the pin
      var checkPinReq = new VerifyPinRequest() { ProcessToken = processToken, Pin = pin };
      httpStatus = client.ExecutePut<VerifyPinRequest, HttpStatusCode>(checkPinReq, "api/passwordreset/pin/verify");
      Assert.AreEqual(HttpStatusCode.OK, httpStatus, "Failed to submit pin.");
      // 5. UI retrieves the process to see if pin was correct and to see further steps. 
      //    If the pin was correct, the email is confirmed, and now we can retrieve the process object; otherwise the call would return null.
      process = client.ExecuteGet<LoginProcess>("api/passwordreset/process?token={0}", processToken);
      Assert.IsNotNull(process, "Failed to retrieve process object.");
      Assert.AreEqual(LoginProcessType.PasswordReset, process.ProcessType, "Process type does not match.");
      Assert.AreEqual(ExtraFactorTypes.Email, process.CompletedFactors, "Expected email as completed factor.");
      Assert.AreEqual(ExtraFactorTypes.SecretQuestions, process.PendingFactors, "Expected SecretQuestions as pending factor.");
      // 6. Next step is in process.PendingFactors - it is secret questions; get Ferb's questions and submit answers.
      var questions = client.ExecuteGet<IList<SecretQuestion>>("api/passwordreset/userquestions?token={0}", processToken);
      Assert.AreEqual(3, questions.Count, "Expected 3 questions");
      //Let's first try incorrect answers
      var ferbAnswers = new List<SecretQuestionAnswer>();
      ferbAnswers.Add(new SecretQuestionAnswer() { QuestionId = questions[0].Id, Answer = "Candice" }); //best childhood friend - incorrect
      ferbAnswers.Add(new SecretQuestionAnswer() { QuestionId = questions[1].Id, Answer = "Potato" });        //favorite food
      ferbAnswers.Add(new SecretQuestionAnswer() { QuestionId = questions[2].Id, Answer = "blue" });       //favorite color
      var answersOk = client.ExecutePut<List<SecretQuestionAnswer>, bool>(ferbAnswers, "api/passwordreset/userquestions/answers?token={0}", processToken);
      Assert.IsFalse(answersOk, "Expected bad answers to fail.");
      //Now correct answers
      ferbAnswers[0].Answer = "Phineas"; //this is correct
      answersOk = client.ExecutePut<List<SecretQuestionAnswer>, bool>(ferbAnswers, "api/passwordreset/userquestions/answers?token={0}", processToken);
      Assert.IsTrue(answersOk, "Expected answers to succeed.");
      // 7. Get the process object - there should be no pending factors
      process = client.ExecuteGet<LoginProcess>("api/passwordreset/process?token={0}", processToken);
      Assert.AreEqual(ExtraFactorTypes.None, process.PendingFactors, "Expected no pending factors");
      // 8. Once all steps are completed, and server cleared all pending factors, the server will allow us to change password
      //   in the context of the process. So let's actually change the password
      var passwordResetReq = new PasswordChangeInfo() {  NewPassword = oldPassword }; //same as the original one
      var success = client.ExecutePut<PasswordChangeInfo, bool>(passwordResetReq, "api/passwordreset/new?token={0}", processToken);
      Assert.IsTrue(success, "Failed to change password");
      // 9. Verify that email notification was sent about password change
      var notifEmail = TestStartup.GetLastMessageTo(ferbEmail);
      Assert.IsNotNull(notifEmail, "Password change notification was not sent.");
      Assert.AreEqual(LoginMessageType.PasswordResetCompleted, notifEmail.MessageType, "Expected password change message.");
      // 10. Try to login with changed password
      ferbLogin = LoginAs(ferbUserName, oldPassword);
      Assert.AreEqual(LoginAttemptStatus.Success, ferbLogin.Status, "Login failed after reset.");


      // ============================ Multi-factor login ==============================================
      // Ferb decides to enable multi-factor login - with email and google authenticator; we need to add GA first as a factor
      loginInfo = client.ExecuteGet<LoginInfo>("api/mylogin");
      Assert.IsNotNull(loginInfo, "Failed to get LoginInfo");
      //Add GoogleAuthenticator factor
      var googleAuth = new LoginExtraFactor() { Type = ExtraFactorTypes.GoogleAuthenticator, Value = null }; // value is ignored, but returned object contains secret
      var gAuth = client.ExecutePost<LoginExtraFactor, LoginExtraFactor>(googleAuth, "api/mylogin/factors");
      var gSecret = gAuth.Value; // Ferb can use it to entry secret manually on his phone
      // Ferb can also use QR reader; 
      var qrUrl = client.ExecuteGet<string>("api/mylogin/factors/{0}/qr", gAuth.Id);
      Assert.IsTrue(!string.IsNullOrWhiteSpace(qrUrl), "Expected QR url.");
      // Find the URL in debug output, paste it in browser address line, see the picture and use Google Authenticator app on your phone
      // to add an account by scanning the QR pic. It should add "BooksEntityApp:ferb" account, and start showing 6 digit code
      Debug.WriteLine("Ferb's QR URL: " + qrUrl);
      //Enable multi-factor login
      loginInfo.Flags |= LoginFlags.RequireMultiFactor;
      loginInfo.MultiFactorLoginFactors = ExtraFactorTypes.Email | ExtraFactorTypes.GoogleAuthenticator;
      client.ExecutePut<LoginInfo, HttpStatusCode>(loginInfo, "api/mylogin");
      Logout();
      // now if Ferb tries to login, he gets multi-factor pending status; the server process (represented by process token) is started automatically
      ferbLogin = LoginAs(ferbUserName, assertSuccess: false);
      Assert.AreEqual(LoginAttemptStatus.PendingMultifactor, ferbLogin.Status, "Expected multi-factor status.");
      processToken = ferbLogin.MultiFactorProcessToken; //the process already started
      Assert.IsFalse(string.IsNullOrEmpty(processToken), "Expected process token");
      // We do not need to conceal the existense of the process like we do in password reset, so request for process returns non-null object 
      process = client.ExecuteGet<LoginProcess>("api/login/multifactor/process?token={0}", processToken);
      Assert.IsNotNull(process, "Expected process object.");
      Assert.AreEqual(ExtraFactorTypes.Email | ExtraFactorTypes.GoogleAuthenticator, process.PendingFactors, "Expected email and Google Auth pending factors.");
      // Email: Ask server to send pin by email
      sendPinRequest = new SendPinRequest() { ProcessToken = processToken, FactorType = ExtraFactorTypes.Email };
      httpStatus = client.ExecutePost<SendPinRequest, HttpStatusCode>(sendPinRequest, "api/login/multifactor/pin/send");
      Assert.AreEqual(HttpStatusCode.OK, httpStatus, "Expected OK status");
      //Get message with pin from mock inbox and extract pin
      pinEmail = TestStartup.GetLastMessageTo(ferbEmail);
      Assert.IsNotNull(pinEmail, "Email with pin not sent.");
      pin = pinEmail.Pin;
      // Ferb copies pin from email and enters it in UI. UI submits the pin 
      checkPinReq = new VerifyPinRequest() { ProcessToken = processToken, Pin = pin };
      success = client.ExecutePut<VerifyPinRequest, bool>(checkPinReq, "api/login/multifactor/pin/verify");
      Assert.IsTrue(success, "Email pin submit failed");
      // Google authenticator. 
      //Tell server to 'send pin' - it won't send anything, but will set GoogleAuth as current factor in the process
      sendPinRequest = new SendPinRequest() { ProcessToken = processToken, FactorType = ExtraFactorTypes.GoogleAuthenticator };
      httpStatus = client.ExecutePost<SendPinRequest, HttpStatusCode>(sendPinRequest, "api/login/multifactor/pin/send");
      // Pretend Ferb has GA installed on his phone, he opens the app and reads the current value. 
      // In this test we use back door and compute it - we know the secret from the call when we added the factor (Google Authenticator as extra factor)
      var gaPassCode = Vita.Modules.Login.GoogleAuthenticator.GoogleAuthenticatorUtil.GeneratePasscode(gSecret);
      //Submit passcode as pin
      checkPinReq = new VerifyPinRequest() { ProcessToken = processToken, Pin = gaPassCode };
      success = client.ExecutePut<VerifyPinRequest, bool>(checkPinReq, "api/login/multifactor/pin/verify");
      Assert.IsTrue(success, "Google authenticator pin failed.");

      //Get process again - now there should be no pending factors
      process = client.ExecuteGet<LoginProcess>("api/login/multifactor/process?token={0}", processToken);
      Assert.AreEqual(ExtraFactorTypes.None, process.PendingFactors, "Expected no pending factors");
      //complete login using process token - returned LoginResponse object represents successful login
      var mfCompleteReq = new MultifactorLoginRequest() { ProcessToken = processToken };
      ferbLogin = client.ExecutePost<MultifactorLoginRequest, LoginResponse>(mfCompleteReq, "api/login/multifactor/complete");
      Assert.IsNotNull(ferbLogin, "Failed to complete login.");
      Assert.AreEqual(LoginAttemptStatus.Success, ferbLogin.Status, "Expected success status");
      client.AddAuthorizationHeader(ferbLogin.AuthenticationToken); //we have to add it explicitly here
      // Ferb identifies the computer as personal (safe) device, and sets to skip multi-factor on this device
      var deviceInfo = new DeviceInfo() { Type = DeviceType.Computer, TrustLevel = DeviceTrustLevel.AllowSingleFactor };
      //register the computer 
      deviceInfo = client.ExecutePost<DeviceInfo, DeviceInfo>(deviceInfo, "api/mylogin/device");
      // The returned token should be saved in local storage and used in future logins
      var deviceToken = deviceInfo.Token;
      //Now let's try to logout and login again, using deviceToken
      Logout();
      ferbLogin = LoginAs(ferbUserName, deviceToken: deviceToken, assertSuccess: false);
      Assert.AreEqual(LoginAttemptStatus.Success, ferbLogin.Status, "Expected no multi-factor on trusted device");
      Logout();
    }

  }//class

} //ns
