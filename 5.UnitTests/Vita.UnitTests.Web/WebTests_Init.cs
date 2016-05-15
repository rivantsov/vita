using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Diagnostics;

using Vita.Common;
using Vita.Web;
using Vita.Modules.Logging;
using Vita.Entities;
using Vita.Modules.Login;
using Vita.Modules.Login.Api;
using Vita.Modules.WebClient.Sync;

namespace Vita.UnitTests.Web {

  [TestClass]
  public partial class WebTests  {

    [TestInitialize]
    public void InitTest() {
      SetupHelper.Init();
    }
    [TestCleanup]
    public void TestCleanup() {
      SetupHelper.FlushLogs();
    }

    //Helper methods used by othere tests
    private LoginResponse LoginAs(string userName, string password = null, bool assertSuccess = true, string deviceToken = null) {
      password = password ?? Samples.BookStore.SampleData.SampleDataGenerator.DefaultPassword;
      var loginRq = new LoginRequest() { UserName = userName, Password = password , DeviceToken = deviceToken};
      var resp = SetupHelper.Client.ExecutePost<LoginRequest, LoginResponse>(loginRq, "api/login");
      Assert.IsTrue(resp != null, "Authentication failed.");
      if(resp.Status == LoginAttemptStatus.Success) {
        SetupHelper.Client.AddRequestHeader("Authorization", resp.AuthenticationToken);
        return resp; 
      }
      if (assertSuccess)
        Assert.IsTrue(false, "Authentication failed, Status: " + resp.Status);
      return resp;
    }

    private void Logout() {
      SetupHelper.Client.ExecuteDelete("api/login");
      SetupHelper.Client.RemoveRequestHeader("Authorization");
    }

  }//class
}
