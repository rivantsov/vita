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

namespace Vita.UnitTests.Web {

  public partial class WebTests  {

    [TestMethod]
    public void TestUserSession() {
      // What is being tested
      // 1. Changed values in user session are immediately saved in the database and session version is incremented
      // 2. LastUsedOn is being updated (on background thread, with 10 seconds increments)
      // 3. Session version higher than current cached causes session refresh
      var client = SetupHelper.Client;
      var timeService = SetupHelper.BooksApp.TimeService;
      //Login as Dora
      var doraLogin = LoginAs("dora");
      var authToken = doraLogin.AuthenticationToken;
      var serverSession = SetupHelper.BooksApp.OpenSystemSession();
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
      SetupHelper.BooksApp.TimeService.SetCurrentOffset(TimeSpan.FromMilliseconds(0)); //restore time 

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
      var client = SetupHelper.Client;
      var resp = LoginAs("dora");
      //set timezone
      var currTimeZoneOffset = TimeZoneInfo.Local.BaseUtcOffset.TotalMinutes;
      client.ExecutePut<object, HttpStatusCode>(null, "/api/login/session/timezoneoffset?minutes={0}", currTimeZoneOffset);
      //get current user info
      var sessionInfo = client.ExecuteGet<SessionInfo>("/api/login/session/info");
      Assert.AreEqual("dora", sessionInfo.UserName, "Expected dora as user name.");
      Assert.AreEqual(currTimeZoneOffset, sessionInfo.TimeZoneOffsetMinutes, "Timezone offset mismatch");
      Logout();
    }


  }//class
}
