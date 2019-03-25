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

namespace Vita.UnitTests.Web {

  [TestClass]
  public partial class WebTests  {

    [TestInitialize]
    public void InitTest() {
      Web.TestStartup.Init();
    }
    [TestCleanup]
    public void TestCleanup() {
      Web.TestStartup.ShutDown();
    }

    //Helper methods used by othere tests
    private LoginResponse LoginAs(string userName, string password = null, bool assertSuccess = true, string deviceToken = null) {
      password = password ?? Samples.BookStore.SampleData.SampleDataGenerator.DefaultPassword;
      var loginRq = new LoginRequest() { UserName = userName, Password = password , DeviceToken = deviceToken};
      var resp = TestStartup.Client.ExecutePost<LoginRequest, LoginResponse>(loginRq, "api/login");
      Assert.IsTrue(resp != null, "Authentication failed.");
      if(resp.Status == LoginAttemptStatus.Success) {
        //We can use AddAuthorizationHeader here as well
        TestStartup.Client.AddAuthorizationHeader(resp.AuthenticationToken);
        return resp; 
      }
      if (assertSuccess)
        Assert.IsTrue(false, "Authentication failed, Status: " + resp.Status);
      return resp;
    }

    private void Logout() {
      TestStartup.Client.ExecuteDelete("api/login");
      TestStartup.Client.RemoveRequestHeader("Authorization");
    }

  }//class
}
