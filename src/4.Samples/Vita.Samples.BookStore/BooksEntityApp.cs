﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.DbInfo;
using Vita.Entities.Services;
using Vita.Entities.Utilities;
using Vita.Modules.EncryptedData;
using Vita.Modules.Login;

namespace Vita.Samples.BookStore {

  public class BooksEntityApp : EntityApp {
    public static BooksEntityApp Instance; 

    public const string CurrentVersion = "1.2.1.0";

    public BooksModule MainModule;

    public BooksEntityApp() : base("BookStore", CurrentVersion) {
      //Areas 
      var booksArea = this.AddArea("books");
      var infoArea = this.AddArea("info");
      var loginArea = this.AddArea("login");

      //main module
      MainModule = new BooksModule(booksArea);
      //Standard modules
      var dbInfoModule = new DbInfoModule(infoArea);

      //Job exec module - disabled for now
      //var jobExecModule = new Modules.JobExecution.JobExecutionModule(booksArea);

      // LoginModule
      var loginStt = new LoginModuleSettings(passwordExpirationPeriod: TimeSpan.FromDays(180));
      loginStt.RequiredPasswordStrength = PasswordStrength.Medium;
      loginStt.DefaultEmailFrom = "team@bookstore.com";

      var loginModule = new LoginModule(loginArea, loginStt);

      // Setup encrypted data module. 
      var encrModule = new EncryptedDataModule(booksArea);
      //  Use TestGenerateCryptoKeys test in BasicTests project to generate 
      // and print crypto keys for all algorithms.
      var cryptoKey = "8E487AD4C490AC43DF15D33AB654E5A222A02C9C904BC51E48C4FE5B7D86F90A";
      var cryptoBytes = HexUtil.HexToByteArray(cryptoKey);
      encrModule.AddChannel(cryptoBytes); //creates default channel

      Instance = this; 
    }

    public override IList<Claim> GetUserClaims(OperationContext userContext) {
      var claims = base.GetUserClaims(userContext);
      var session = userContext.OpenSession();
      using (session.WithElevatedRead()) {
        var userId = userContext.User.UserId; 
        var user = session.GetEntity<IUser>(userId);
        Util.Check(user != null, "User not found, user id: {0}", userId);
        claims.AddRole(user.Type.ToString());
      }
      return claims; 
    }

  }//class
}//ns
