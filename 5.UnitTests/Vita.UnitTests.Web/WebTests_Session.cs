using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Threading;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Web;
using Vita.UnitTests.Common;
using Vita.Modules.Email;
using Vita.Modules.Logging;
using Vita.Samples.BookStore;
using Vita.Samples.BookStore.Api;
using Vita.Modules.WebClient.Sync;
using Vita.Modules.Login.Api;
using Vita.Modules.Logging.Api;
using Vita.Entities.Web;

namespace Vita.UnitTests.Web {

  public partial class WebTests  {

    [TestMethod]
    public void TestUserSession() {
      // What is being tested
      // 1. Changed values in user session are immediately saved in the database and session version is incremented
      // 2. LastUsedOn is being updated (on background thread, with 10 seconds increments)
      // 3. Session version higher than current cached causes session refresh
      var client = Startup.Client;
      var timeService = Startup.BooksApp.TimeService;
      //Login as Dora
      var doraLogin = LoginAs("dora");
      var authToken = doraLogin.AuthenticationToken;
      var serverSession = Startup.BooksApp.OpenSystemSession();
      var sessionRec = serverSession.EntitySet<IUserSession>().Where(s => s.WebSessionToken == authToken).FirstOrDefault();
      Assert.IsNotNull(sessionRec, "Session record not found.");
      var oldVersion = sessionRec.Version;
      client.ExecutePost<object, HttpStatusCode>(null, "/api/special/sessionvalue?name=varName&value=varValue1");
      //Read session rec
      EntityHelper.RefreshEntity(sessionRec);
      var serValues = sessionRec.Values;
      Assert.IsTrue(serValues.Contains("varName"), "Expected serialized values to contain 'varName'.");
      Assert.IsTrue(serValues.Contains("varValue1"), "Expected serialized values to contain 'varValue1'.");
      var newVersion = sessionRec.Version;
      var oldLastUsed = sessionRec.LastUsedOn;
      Assert.IsTrue(newVersion > oldVersion, "Expected version increment.");
      //Force updating lastUsedOn value: fast forward time one minute, make a call
      timeService.SetCurrentOffset(TimeSpan.FromMinutes(1));
      //var varValue = client.ExecuteGet<string>("/api/special/sessionvalue?name=varName");
      var varValue = client.ExecuteGet<string>("/api/special/sessionvalue?name=varName");
      Assert.AreEqual("varValue1", varValue, "Expected 'varValue1' as a value."); 
      // The last API call came a minute later after the first one (according to time service); 
      // The userSesson.LastUsedOn value is updated with 10 seconds increments, so it should be updated in memory 
      // and scheduled with background service to update in the database. BackgroundSave service runs every 1 second.
      // Wait for 2 seconds to let db update execute, and check the value. 
      Thread.Sleep(2000);
      EntityHelper.RefreshEntity(sessionRec);
      var newLastUsed = sessionRec.LastUsedOn;
      Assert.IsTrue(oldLastUsed.AddSeconds(50) < newLastUsed, "Expected new last used to be a minute away.");
      //set time back
      Startup.BooksApp.TimeService.SetCurrentOffset(TimeSpan.FromMilliseconds(0)); //restore time 

      // Test that cached user session is refreshed if incoming session version is higher. 
      // Scenario: we pretend that call goes through another web server and changes foo value in the session;
      // UserSession in the database is updated, session version is incremented and new version is returned in 
      // X-Versions header. If we make a new call with this new version value in the header, and it goes to another 
      // web server in a farm with stale cache of user session, then web server should recognize from higher version
      // that cache is stale, and it would reload session from the database.  
      // We simulate this scenario by changing value directly in the database (pretending it's done thru other web server)
      //Change foo value in the database - pretend a call thru another web server updated it. 
      // Then we hand-craft a version token with incremented session version (we pretend the call thru another server 
      // returned this incremented value). If we make a call with this higher version value, the server should detect 
      // that user session in cache is stale and it needs to be refreshed. It will refresh it and will return a new
      // fooValue. 
      // First let's get the old value, and get the version token
      string incomingVersionToken = null; 
      var xVersions = "X-Versions";
      client.Settings.ResponseSpy = r => { incomingVersionToken = r.GetHeaderValue(xVersions); }; //setup spy
      varValue = client.ExecuteGet<string>("/api/special/sessionvalue?name=varname");
      client.Settings.ResponseSpy = null; 
      Assert.AreEqual("varValue1", varValue, "Expected 'varValue1' as a value.");
      var versionArr = incomingVersionToken.Split(',').Select(s => int.Parse(s)).ToArray();
      //Change value directly in the database by replacing it in XML (serialized value set)
      sessionRec.Values = serValues.Replace("varValue1", "varValue2");
      serverSession.SaveChanges();
      // After update, the current session version should be incremented, and returned with version token
      // Pretend we have new versions token
      versionArr[0] = versionArr[0] + 1;
      var newVersionToken = string.Join(",", versionArr);
      client.AddRequestHeader(xVersions, newVersionToken);
      varValue = client.ExecuteGet<string>("/api/special/sessionvalue?name=varname");
      Assert.AreEqual("varValue2", varValue, "Expected 'varValue2' as a value.");
      client.RemoveRequestHeader(xVersions); 

      //clean up
      // delete all web call entries made when time was one-minute forward; otherwise, other tests will fail 
      // (some tests get 'latest' web call and check it, so these future entries would break them)
      var delQuery = serverSession.EntitySet<IWebCallLog>().Where(wc => wc.CreatedOn > timeService.UtcNow);
      delQuery.ExecuteDelete<IWebCallLog>(); 
    }

    [TestMethod]
    public void TestSessionInfoApi() {
      var client = Startup.Client;
      var resp = LoginAs("dora");
      //set timezone
      var currTimeZoneOffset = TimeZoneInfo.Local.BaseUtcOffset.TotalMinutes;
      client.ExecutePut<object, HttpStatusCode>(null, "/api/usersession/client-timezone-offset?minutes={0}", currTimeZoneOffset);
      //get current user info
      var sessionInfo = client.ExecuteGet<UserSessionInfo>("/api/usersession");
      Assert.AreEqual("dora", sessionInfo.UserName, "Expected dora as user name.");
      Assert.AreEqual(currTimeZoneOffset, sessionInfo.TimeOffsetMinutes, "Timezone offset mismatch");
      Logout();
    }

    // Test user session with different expiration types
    [TestMethod]
    public void TestSessionExpiration() {
      try {
        // do it several times, there was a bug initially that occurred not consistently
        for(int i = 0; i < 5; i++)
          TestSessionExpirationImpl();
        //Test login with long and no expiration option (for mobile devices)
        TestSessionWithLongExpiration();
      } finally {
        // make sure time offset in TimeService is set back to zero
        Startup.BooksApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
        Logout();
      }

    }

    private void TestSessionExpirationImpl() {
      var client = Startup.Client;
      var timeService = Startup.BooksApp.TimeService; //it is shared with LoggingApp
      var dora = LoginAs("dora");
      var doraUser = client.ExecuteGet<User>("api/user"); //get current user
      //session expires in 20 minutes; move clock forward by 1 hour; the session should expire and any call requiring authentication would fail
      timeService.SetCurrentOffset(TimeSpan.FromHours(1));
      System.Threading.Thread.Sleep(100);
      var faultExc = TestUtil.ExpectClientFault(() => client.ExecuteGet<User>("api/user")); // now it should fail
      var faultCode = faultExc.Faults[0].Code;
      Assert.AreEqual(ClientFaultCodes.AuthenticationRequired, faultCode, "Expected AuthenticationRequired fault.");
      timeService.SetCurrentOffset(TimeSpan.Zero);
    }//method

    private void TestSessionWithLongExpiration() {
      var client = Startup.Client;
      var timeService = Startup.BooksApp.TimeService; //it is shared with LoggingApp
      var utcNowReal = timeService.UtcNow;

      // 1. Test no-expiration option
      var dora = LoginAs("dora", expirationType: UserSessionExpirationType.KeepLoggedIn);
      //try getting current user session, should go ok
      var doraSession = client.ExecuteGet<UserSessionInfo>("api/usersession"); //get current user session
      //session does not expire; move clock forward by 5 years; the session should still be ok
      timeService.SetCurrentOffset(TimeSpan.FromDays(365 * 5));
      doraSession = client.ExecuteGet<UserSessionInfo>("api/usersession"); //get current user session
      timeService.SetCurrentOffset(TimeSpan.Zero);
      Logout();

      // 2. Test long expiration, with token refresh
      dora = LoginAs("dora", expirationType: UserSessionExpirationType.LongFixedTerm); //fixed expiration in a month
      //try getting current user, should go ok
      doraSession = client.ExecuteGet<UserSessionInfo>("api/usersession"); //get current user session
      Assert.IsTrue(doraSession.Expires > utcNowReal.AddDays(29), "Expected expires in a month");
      //now move clock forward by 25 days and refersh token; the session should still be ok
      timeService.SetCurrentOffset(TimeSpan.FromDays(25));
      //Refresh session token - get new one with new expiration date - a month from 'shifted current'
      var refreshRequest = new RefreshRequest() { RefreshToken = dora.RefreshToken };
      var refreshResponse = client.ExecutePut<RefreshRequest, RefreshResponse>(refreshRequest, "api/usersession/token");
      client.AddAuthorizationHeader(refreshResponse.NewSessionToken); //put it into auth header
      // now get session again and check expiration
      doraSession = client.ExecuteGet<UserSessionInfo>("api/usersession"); //get current user session
      Assert.IsTrue(doraSession.Expires > utcNowReal.AddDays(29 + 25), "Expected expires in 25 days + month");
    }



  }//class
}
