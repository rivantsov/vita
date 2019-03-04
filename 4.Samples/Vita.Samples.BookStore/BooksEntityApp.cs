using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.DbInfo;
using Vita.Entities.Services;
using Vita.Modules.Login;

namespace Vita.Samples.BookStore {

  public class BooksEntityApp : EntityApp {
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

      /* SlimApi not migrated yet
      //api config - register controllers defined in Vita.Modules.Login assembly; books controllers are registered by BooksModule
      base.ApiConfiguration.RegisterControllerTypes(
        typeof(LoginController), typeof(PasswordResetController), typeof(LoginSelfServiceController), typeof(LoginAdministrationController)
        );
        */
    }

  }//class
}//ns
