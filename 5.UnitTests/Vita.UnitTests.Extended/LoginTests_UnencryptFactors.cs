using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common;
using Vita.Entities;
using Vita.Modules.Login;
using Vita.Modules.EncryptedData; 


namespace Vita.UnitTests.Extended {


  public partial class LoginTests {

    // This is a test for migration method for 1.2 version - login factors no longer encrypted; migration method unecrypts existing logins
    [TestMethod]
    public void TestLoginUnencryptFactors() {
      var app = Startup.BooksApp;
      var session = app.OpenSystemSession();
      //create couple of encrypted factors
      var loginStt = app.GetConfig<LoginModuleSettings>();
      var loginSrv = app.GetService<ILoginManagementService>();
      var stanLogin = session.EntitySet<ILogin>().First(lg => lg.UserName == "stan");
      var encrStanEmail1 = session.NewOrUpdate(null, "stan1@email.com", loginStt.EncryptionChannelName);
      var encrStanEmail2 = session.NewOrUpdate(null, "stan2@email.com", loginStt.EncryptionChannelName);
      //we need to save it before assigning to factor - auto-sorting of updates would not work in this case (we ref by info_id!)
      session.SaveChanges(); 
      //create factor records
      var f1 = session.NewEntity<ILoginExtraFactor>();
      f1.Login = stanLogin;
      f1.FactorType =  ExtraFactorTypes.Email;
      f1.Info_Id = encrStanEmail1.Id;
      var f2 = session.NewEntity<ILoginExtraFactor>();
      f2.Login = stanLogin;
      f2.FactorType = ExtraFactorTypes.Email;
      f2.Info_Id = encrStanEmail2.Id;
      session.SaveChanges();

      // Now call decrypting method used in migrations
      session = app.OpenSystemSession();
      LoginModule.UnencryptFactorValues(session);
      // check that values now are unencrypted
      session = app.OpenSystemSession();
      f1 = session.GetEntity<ILoginExtraFactor>(f1.Id);
      Assert.AreEqual("stan1@email.com", f1.FactorValue, "Factor value does not match.");
      f2 = session.GetEntity<ILoginExtraFactor>(f2.Id);
      Assert.AreEqual("stan2@email.com", f2.FactorValue, "Factor value does not match.");

    }
  }


}
