using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Entities.Runtime; 
using Vita.Samples.BookStore;
using Vita.Modules.Login;
using Vita.Common;
using System.Globalization;
using Vita.Modules.Login.Api;

namespace Vita.UnitTests.Extended {


  [TestClass]
  public partial class LoginTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      Startup.TearDown(); 
    }

    [TestMethod]
    public void TestLoginAdvancedFeatures() {
      var password = Samples.BookStore.SampleData.SampleDataGenerator.DefaultPassword; 
      var app = Startup.BooksApp;
      var loginService = app.GetService<ILoginService>();
      var loginMgr = app.GetService<ILoginManagementService>(); 
      var loginProcessService = app.GetService<ILoginProcessService>();
      var loginAdmin = app.GetService<ILoginAdministrationService>(); 
      var context = app.CreateSystemContext(); 

      // Simple login/logout ------------------------------------------------------------------------
      //Let's try to login Dora
      var doraEmail = "dora@email.com";
      var doraLogin = loginService.Login(context, "dora", "invalid password"); // it is Dora, but we configured for case-insensitive user names
      Assert.AreEqual(LoginAttemptStatus.Failed, doraLogin.Status, "Expected login fail.");
      doraLogin = loginService.Login(context, "dora", password);
      Assert.AreEqual(LoginAttemptStatus.Success, doraLogin.Status, "Expected login succeed.");
      var doraUser = doraLogin.User;
      loginService.Logout(context); // should write a log entry

      // Password reset, full process. --------------------------------------------------------
      // See detailed discussion here: http://www.troyhunt.com/2012/05/everything-you-ever-wanted-to-know.html
      // Let's say Dora forgot her password. She comes to Login page and clicks 'Forgot password' link.
      // She is presented with a box 'Enter your email', and a captcha to verify she's not a robot. 
      // Dora enters email, solves captcha and clicks NEXT. We verify captcha (outside this sample) 
      //if captcha matches, we proceed to search for login record using email
      var session = app.OpenSystemSession();
      var enteredEmail = doraEmail;

      var doraEmailFactor = loginProcessService.FindLoginExtraFactor(context, ExtraFactorTypes.Email, enteredEmail);
      // In this test, we know login exists; in real app, if we do not find login, stop here, 
      // but do not disclose that email did not match; say 'Reset URL was sent to this email if it was found in our database'
      Assert.IsNotNull(doraEmailFactor, "Expected to find login by email.");
      // login is found; Start login process
      var processToken = loginProcessService.GenerateProcessToken(); 
      var process = loginProcessService.StartProcess(doraEmailFactor.Login, LoginProcessType.PasswordReset, processToken);

      Assert.AreEqual(ExtraFactorTypes.Email | ExtraFactorTypes.SecretQuestions, process.PendingFactors, "Expected Email and Secret questions pending factors");
      // send email to email address provided by user with a link containing the flowToken; wait for the user to hit the link.
      // Do not send anything if login factor was not found by email; otherwise your site becomes email DDOS bot
      // Important: in both cases (email found or not), present user (dora or not) with the same page 
      // saying 'Reset instructions were sent to email you provided, if it was found in our database. ', without disclosing if email was found or not
      // Embed process token in URL and send it in email
      loginProcessService.SendPin(process, doraEmailFactor);

      //Dora receives email, copies pin
      var emailMsg = Startup.GetLastMessageTo(doraEmail);
      var pin = (string) emailMsg.Parameters[LoginNotificationKeys.Pin]; //get pin
      //Find the login process
      session = app.OpenSystemSession(); 
      process = loginProcessService.GetActiveProcess(context, LoginProcessType.PasswordReset, processToken);
      // if the process is null, present with page 'oopss..' invalid link or link expired
      Assert.IsNotNull(process, "Expected to find process.");
      loginProcessService.SubmitPin(process, pin); 

      // Next - secret questions
      Assert.AreEqual(ExtraFactorTypes.SecretQuestions, process.PendingFactors, "Expected Secret questions pending factor.");
      var qaList = process.Login.SecretQuestionAnswers;
      Assert.AreEqual(3, qaList.Count, "Expected 3 questions/answers");
      //present Dora with a page with her 3 questions, wait until she types the answers
      // Assume we got the answers; we also have flowToken preserved somewhere on the page
      qaList = process.Login.SecretQuestionAnswers;
      var answers = new SecretQuestionAnswer[] {
        new SecretQuestionAnswer() {QuestionId = qaList[0].Question.Id, Answer = "Diego"}, //best friend
        new SecretQuestionAnswer() {QuestionId = qaList[1].Question.Id, Answer = "Banana"},//favorite fruit
        new SecretQuestionAnswer() {QuestionId = qaList[2].Question.Id, Answer = "yellow"},//favorite color
      };
      var answersCorrect = loginProcessService.CheckAllSecretQuestionAnswers(process, answers);
      Assert.IsTrue(answersCorrect, "Secret question answers failed.");
      process = loginProcessService.GetActiveProcess(context, LoginProcessType.PasswordReset, processToken);
      Assert.AreEqual(ExtraFactorTypes.None, process.PendingFactors, "Expected no pending factors.");
      // Dora enters new password and hits Submit
      // Let's test detection of reuse of old password
      var oldPass = password;
      bool wasUsed = loginMgr.CheckPasswordWasUsed(process.Login, oldPass, TimeSpan.FromDays(365), 5);
      Assert.IsTrue(wasUsed, "Failed to detect password reuse.");
      // Now set new password
      var newPass = password + "New";
      wasUsed = loginMgr.CheckPasswordWasUsed(process.Login, newPass, TimeSpan.FromDays(365), 5);
      Assert.IsFalse(wasUsed, "False reuse signal detected.");
      loginMgr.ChangePassword(process.Login, oldPass, newPass);
      //we are done; let's try to login Dora with new password
      doraLogin = loginService.Login(context, "dora", newPass); //user names are case insensitive
      Assert.IsTrue(doraLogin.Status == LoginAttemptStatus.Success, "Failed to login after password change.");
      //Change back, to avoid breaking other tests
      loginMgr.ChangePassword(process.Login, newPass, oldPass);

/*      // Quick test of a bug (LastLoggedIn not updated with one-time pwd)
      var tempPass = "abcd1234";
      loginAdmin.SetOneTimePassword(doraLogin.Login, tempPass);
      doraLogin = loginService.Login(context, "dora", tempPass);
      loginMgr.ChangePassword(doraLogin.Login, tempPass, oldPass);
*/
    }//method

    [TestMethod]
    public void TestLoginFailedTrigger() {
      var password = Samples.BookStore.SampleData.SampleDataGenerator.DefaultPassword;
      var app = Startup.BooksApp;
      var loginService = app.GetService<ILoginService>();
      if(loginService == null)
        return;
      var context = new OperationContext(app, UserInfo.System);
      //first login normally - make sure login works
      var duffyLogin = loginService.Login(context, "Duffy", password);
      Assert.IsTrue(duffyLogin.Status == LoginAttemptStatus.Success);
      loginService.Logout(context); 

      //Make 3 bad login attempts
      for(int i = 0; i < 5; i++) {
        duffyLogin = loginService.Login(context, "Duffy", "not-a-pass");
        Assert.IsFalse(duffyLogin.Status == LoginAttemptStatus.Success);
      }
      //account shoud be suspended by LoginFailedTrigger
      duffyLogin = loginService.Login(context, "Duffy", password);
      Assert.IsFalse(duffyLogin.Status == LoginAttemptStatus.Success, "Duffy should be suspended");
   
    }

    // Extracting pin. Pin in email templates is always preceeded by ':' and followed by New-line or EOF
    private static string ExtractPin(string body) {
      var start = body.IndexOf(':');
      Assert.IsTrue(start >= 0, "Failed to extract pin, ':' not found. Body: " + body);
      var end = body.IndexOf(Environment.NewLine, start);
      if(end == -1)
        end = body.Length;
      var pin = body.Substring(start + 1, end - start - 1);
      return pin.Trim();
    }

  }//class
}
