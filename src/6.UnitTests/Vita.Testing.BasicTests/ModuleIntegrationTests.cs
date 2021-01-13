using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Testing.BasicTests.ModuleIntegration {

  // This code is a demo and test for the entity modules integration in VITA framework.
  // It models a simplistic scenario when 2 programmers independently develop entity 
  // modules, and the 3d programmer integrates these module into his application. 
  
  // Step 1: Alice creates a LoginModule that handles the table of user login information.
  // Knowing that login record should normally reference the Users table that contains full info about user,
  // she defines and uses a stub entity IUserStub that will be replaced by a real User entity when application is assembled.
  namespace Alice {
    [Entity]
    public interface ILogin {
      [PrimaryKey, Auto]
      Guid Id { get; set; }
      string UserName { get; set; }
      int PasswordHash { get; set; }
      string FriendlyName { get; set; }
      IUserStub User { get; set; }
    }

    [Entity]
    public interface IUserStub { } // an empty stub to be replaced

    public class LoginModule : EntityModule {
      // Alice created a constructor with extra parameter userEntityType 
      // suggesting to provide a real type of User entity in the system.
      public LoginModule(EntityArea area, Type userEntityType) : base(area, "Login") {
        RegisterEntities(typeof(ILogin), typeof(IUserStub)); //still need to register IUserStub, to immediately replace it
        var serv = App.GetService<IEntityModelCustomizationService>(); 
        serv.ReplaceEntity(typeof(IUserStub), userEntityType);
      }

      //Alice defines a utility method to login users. 
      public static ILogin Login(IEntitySession session, string userName, 
                        int passwordHash) {
        // For simplicity, we use direct LINQ
        var query = from login in session.EntitySet<ILogin>()
                    where login.UserName == userName && 
                          login.PasswordHash == passwordHash
                    select login;
        var logins = query.ToList(); 
        return logins.Count == 0 ? null : logins[0];
      }
    }
  } 

  // Step 2: Bob, working independently from Alice, creates a PersonModule that handles Person.
  namespace Bob {
    public enum Gender {
      Male,
      Female,
    }
    [Entity]
    public interface IPerson {
      [PrimaryKey, Auto]
      Guid Id { get; set; }
      string FirstName { get; set; } //default length would settings default, 32
      string LastName { get; set; }
      Gender Gender { get; set; }
    }

    public class PersonModule : EntityModule {
      public PersonModule(EntityArea area) : base(area, "PersonModule") {
        RegisterEntities(typeof(IPerson));
      }

    }//class
  }//ns 

  // Step 3: Randy develops an application and wants to use LoginModule from Alice and PersonModule from Bob.
  // He needs to integrate both modules with each other and with his own app.
  // He also needs to extend the entities defined in both modules, to add a few custom fields. 
  // Note that at this point Randy is at full control of how Alice's and Bob's entities appear in his application database:
  // their activation behavior is driven by setup objects he creates for his solution. 
  namespace Randy {
    using Gender = Bob.Gender;  //import enum definition

    // Randy wants to add two extra fields to Alice's and Bob's entities. 
    // He also wants Alice's ILogin entity to point to his (Randy's) 
    // IPerson entity which extends Bob's IPerson. 
    [Entity]
    public interface IAppLogin : Alice.ILogin {
      [Unique]
      string EmployeeNumber { get; set; }
    }

    // We are extending IPerson; the reason to inherit from IUserStub is to allow 
    // assignments login.User = personExt;   IUserStub is a stub type to be replaced by real entity type.
    [Entity]
    public interface IPersonExt : Bob.IPerson, Alice.IUserStub {
      DateTime BirthDate { get; set; }
    }

    [TestClass]
    public class ModuleIntegrationTests {
      EntityApp _app; 

      [TestCleanup]
      public void TestCleanup() {
        if(_app != null)
          _app.Flush();
      }

      [TestMethod]
      public void TestModuleIntegration() {
        var schema = "usr";

        // Runs integrated app by Randy
        _app = new EntityApp();
        var area = _app.AddArea(schema); 
        var persModule = new Bob.PersonModule(area);
        var loginModule = new Alice.LoginModule(area, typeof(IPersonExt));
        // Now replace original entities with new interfaces; 
        // Alice's IUserStub is already replaced by Randy's IPersonExt.
        var custModelService = _app.GetService<IEntityModelCustomizationService>();
        custModelService.ReplaceEntity(typeof(Alice.ILogin), typeof(IAppLogin));
        custModelService.ReplaceEntity(typeof(Bob.IPerson), typeof(IPersonExt));
        // activate
        Startup.ActivateApp(_app);

        //Test the resulting solution
        var session = _app.OpenSession();
        var pers = session.NewEntity<IPersonExt>();
        pers.FirstName = "John";
        pers.LastName = "Dow";
        pers.BirthDate = new DateTime(1970, 5, 1);
        pers.Gender = Gender.Male; 
        var login = session.NewEntity<IAppLogin>();
        var loginId = login.Id; 
        login.User = pers;
        login.UserName = "johnd";
        login.FriendlyName = "JohnD";
        login.PasswordHash = 123;
        login.EmployeeNumber = "E111"; 
        session.SaveChanges(); 

        //Let's try to login the user we created using Alice's method
        session = _app.OpenSession();
        var johnLogin = Alice.LoginModule.Login(session, "johnd", 123);
        var cmd = session.GetLastCommand(); 
        Assert.IsNotNull(johnLogin, "Login failed"); 

      }//method

    }//class IntegratedAppByRandy

  }
}
