using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Authorization;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Web;
using Vita.Modules.DbInfo;
using Vita.Modules.EncryptedData;
using Vita.Modules.Logging;
using Vita.Modules.Logging.Api;
using Vita.Modules.Login;
using Vita.Modules.Login.Api;
using Vita.Modules.TextTemplates;
using Vita.Samples.BookStore.Api;

namespace Vita.Samples.BookStore {

  public class BooksEntityApp : EntityApp {
    public const string CurrentVersion = "1.1.0.0";

    public BooksModule MainModule;
    // To use a separate database for logs, change log connection string in app.config file to point to a separate database.
    public LoggingEntityApp LoggingApp;
    public BooksAuthorization Authorization;


    //Main constructor
    public BooksEntityApp(string cryptoKey) : this() {
      var cryptoService = this.GetService<IEncryptionService>(); //created in other constructor 'this()'
      var cryptoBytes = HexUtil.HexToByteArray(cryptoKey);
      cryptoService.AddChannel(cryptoBytes); //sets up default unnamed channel
    }

    // We need to have a parameterless constructor (it might be internal) if we want to use vdbtool to generate DDL scripts
    internal BooksEntityApp() : base("BookStore", CurrentVersion) {
      //Areas 
      var booksArea = this.AddArea("books");
      var infoArea = this.AddArea("info");
      var loginArea = this.AddArea("login");

      //main module
      MainModule = new BooksModule(booksArea);
      //Other modules
      var dbInfoModule = new DbInfoModule(infoArea);
      var cryptModule = new EncryptedDataModule(loginArea); //EncryptedData is used by login module
      var templateModule = new TemplateModule(booksArea);
      // LoginModule
      var loginStt = new LoginModuleSettings(passwordExpirationPeriod: TimeSpan.FromDays(180));
      loginStt.RequiredPasswordStrength = PasswordStrength.Medium;
      loginStt.DefaultEmailFrom = "team@bookstore.com";

      var loginModule = new LoginModule(loginArea, loginStt);
      //Notification service
      var notificationService = new Vita.Modules.Notifications.NotificationService(this);
      // Authorization object - uses LoginModule's authorization objects
      Authorization = new BooksAuthorization(this, loginModule); 
      //api config - register controllers defined in Vita.Modules assembly; books controllers are registered by BooksModule
      base.ApiConfiguration.RegisterControllerTypes( 
        typeof(LoginController), typeof(PasswordResetController), typeof(LoginSelfServiceController), typeof(LoginAdministrationController),
        typeof(ClientErrorController),  typeof(LoggingDataController), typeof(EventsPostController),
        typeof(DiagnosticsController)
        );

      //logging app - linked to main app
      this.LoggingApp = new LoggingEntityApp("log");
      LoggingApp.LinkTo(this);

      //short expir period, just for testing
      var sessionStt = LoggingApp.GetConfig<UserSessionSettings>();
      sessionStt.SessionTimeout = TimeSpan.FromMinutes(5); 
    }

    //Provides user roles for a given user
    public override IList<Role> GetUserRoles(UserInfo user) {
      switch(user.Kind) {
        case UserKind.Anonymous:
          var roles = new List<Role>(); 
          roles.Add(Authorization.AnonymousUser);
          return roles; 
        case UserKind.AuthenticatedUser:
          var session = this.OpenSystemSession(); 
          var iUser = session.GetEntity<IUser>(user.UserId); 
          return Authorization.GetRoles(iUser.Type);
      }
      return new List<Role>(); //never happens
    }

  }//class
}//ns
