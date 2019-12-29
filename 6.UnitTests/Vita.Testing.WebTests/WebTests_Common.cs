using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using Vita.Modules.Login;
using Arrest.Sync;

namespace Vita.UnitTests.Web {

  [TestClass]
  public partial class WebTests  {

    [ClassInitialize]
    public static void InitTest(TestContext ctx) {
      Web.Startup.Init();
    }

    [ClassCleanup]
    public static void TestCleanup() {
      Web.Startup.ShutDown();
    }

    //Helper methods used by othere tests
    private LoginResponse LoginAs(string userName, string password = null, bool assertSuccess = true, string deviceToken = null) {
      password = password ?? Samples.BookStore.SampleData.SampleDataGenerator.DefaultPassword;
      var loginRq = new LoginRequest() { UserName = userName, Password = password , DeviceToken = deviceToken};
      var resp = Startup.Client.Post<LoginRequest, LoginResponse>(loginRq, "api/login");
      Assert.IsTrue(resp != null, "Authentication failed.");
      if(resp.Status == LoginAttemptStatus.Success) {
        //We can use AddAuthorizationHeader here as well
        Startup.Client.AddAuthorizationHeader(resp.AuthenticationToken);
        return resp; 
      }
      if (assertSuccess)
        Assert.IsTrue(false, "Authentication failed, Status: " + resp.Status);
      return resp;
    }

    private void Logout() {
      Startup.Client.Delete("api/login");
      Startup.Client.RemoveRequestHeader("Authorization");
    }

  }//class
}
