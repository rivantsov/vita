using Arrest.Sync;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vita.Modules.Login;

namespace Vita.Testing.WebTests {

  [TestClass]
  public partial class BooksApiTests  {

    [ClassInitialize]
    public static void InitTest(TestContext ctx) {
      Startup.Init();
    }

    [ClassCleanup]
    public static void TestCleanup() {
      Startup.ShutDown();
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
