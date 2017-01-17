using System;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Data;
using Vita.Data.Driver;
using Vita.UnitTests.Common;
using Vita.Entities.Model;

namespace Vita.UnitTests.Basic.MiscTests {


  [Entity, OrderBy("Model")]
  [System.ComponentModel.Description("Represents vehicle entity.")] //Description attr is defined twice, ambigous
  public interface IVehicle {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Size(30)]
    //we test that these attributes will be passed to the object's property
    [System.ComponentModel.Description("Model of the vehicle.")] //Description attr is defined twice, ambigous
    [Browsable(true), DisplayName("Vehicle model"), System.ComponentModel.CategoryAttribute("Miscellaneous")]
    string Model { get; set; }

    int Year { get; set; }

    [PropagageUpdatedOn]
    IDriver Owner { get; set; }
    [Nullable]
    IDriver Driver { get; set; }
  }

  [Entity]
  public interface IDriver {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Utc, Auto(AutoType.UpdatedOn)]
    DateTime UpdatedOn { get; }

    [Size(30), Unique(Alias = "LicenseNumber")]
    string LicenseNumber { get; set; }

    [Size(Sizes.Name)]
    string FirstName { get; set; }
    [Size(Sizes.Name)]
    string LastName { get; set; }

    [OneToMany("Owner")]
    IList<IVehicle> Vehicles { get; set; }

    [OneToMany("Driver")]
    IList<IVehicle> DrivesVehicles { get; set; }

    //DependsOn is optional, used for auto PropertyChanged firing
    [Computed(typeof(MiscTestsExtensions), "GetFullName"), DependsOn("FirstName,LastName")] 
    string FullName { get; }
    // another computer prop - test for a reported bug
    [Computed(typeof(MiscTestsExtensions), "GetLastFirst"), DependsOn("FirstName,LastName")]
    string LastFirst { get; }

    //Persistent computed property
    [Computed(typeof(MiscTestsExtensions), "GetLicenseHash", Persist=true), DependsOn("LicenseNumber")]
    int LicenseHash { get; }

    //Temporary, test for bug fix
    [OneToMany(), Nullable]
    IDriver Instructor { get; set; }
  }

  // We skip defining custom entity module and use base EntityModule class
  public class MiscTestsEntityApp : EntityApp {
    public MiscTestsEntityApp() {
      var area = AddArea("misc");
      var mainModule = new EntityModule(area, "MainModule");
      mainModule.RegisterEntities(typeof(IVehicle), typeof(IDriver));
    }
  }//class

  public static class MiscTestsExtensions {
    public static IVehicle NewVehicle(this IEntitySession session, string model, int year, IDriver owner, IDriver driver = null) {
      var veh = session.NewEntity<IVehicle>();
      veh.Model = model;
      veh.Year = year;
      veh.Owner = owner;
      veh.Driver = driver;
      return veh;
    }
    public static IDriver NewDriver(this IEntitySession session, string licenseNumber, string firstName, string lastName) {
      var driver = session.NewEntity<IDriver>();
      driver.LicenseNumber = licenseNumber;
      driver.FirstName = firstName;
      driver.LastName = lastName;
      return driver; 
    }
    public static string GetFullName(IDriver driver) {
      return driver.FirstName + " " + driver.LastName; 
    }
    public static string GetLastFirst(IDriver driver) {
      return driver.LastName + ", " + driver.FirstName;
    }


    public static int GetLicenseHash(IDriver driver) {
      return Util.StableHash(driver.LicenseNumber);
    }
  }


  [TestClass]
  public class MiscTests {
    EntityApp _app;

    public void DeleteAll() {
      Startup.DeleteAll(_app, typeof(IVehicle), typeof(IDriver)); //, typeof(IStateProvince));
    }

    [TestInitialize]
    public void Init() {
      if(_app == null) {
        Startup.DropSchemaObjects("misc");
        _app = new MiscTestsEntityApp();
        Startup.ActivateApp(_app);
      }
    }

    [TestCleanup]
    public void TestCleanup() {
      if(_app != null)
        _app.Flush();
    }

    [TestMethod]
    public void TestEntityReferences() {
      DeleteAll();
      var session = _app.OpenSession();

      // Quick test for fix of a bug - entityRef entities are not null (initialized with stubs) on new entities
      var carx = session.NewEntity<IVehicle>();
      var carxOwner = carx.Owner;
      Assert.IsNull(carxOwner, "EntityRef on new entity is not null!");

      session = _app.OpenSession();
      var john = session.NewDriver("D001", "John", "Dow");
      var johnId = john.Id;

      var jane = session.NewDriver("D002", "Jane", "Crane");
      var janeId = jane.Id;

      var veh1 = session.NewVehicle("Beatle", 2000, john, john);
      var car1Id = veh1.Id; 
      var veh2 = session.NewVehicle("Explorer", 2005, john, john);
      var car2Id = veh2.Id;
      session.SaveChanges();
      //test for a bug fix - New entities were not tracked after save
      var john2 = session.GetEntity<IDriver>(johnId);
      Assert.IsTrue(john == john2, "new/saved entity is not tracked.");

      //Start new session, load all and check that IDs and relationships are correct
      session = _app.OpenSession();
      john = session.GetEntity<IDriver>(johnId);
      Assert.IsNotNull(john, "John is not found.");
      Assert.AreEqual(2, john.Vehicles.Count, "Invalid # of cars with John.");
      veh1 = john.Vehicles[0];
      veh2 = john.Vehicles[1];
      Assert.IsTrue(veh1.Owner == john, "Car1 owner expected to be John");
      Assert.IsTrue(veh2.Owner == john, "Car2 owner expected to be John");
      Assert.IsTrue(veh1.Driver == john, "Car1 driver expected to be John");
      Assert.IsTrue(veh2.Driver == john, "Car2 driver expected to be John");
      //Transfer car2 to jane
      jane = session.GetEntity<IDriver>(janeId);
      veh2.Owner = jane; 
      veh2.Driver = null;
      session.SaveChanges();
      //immediately verify that vehicle lists in persons are refreshed
      Assert.IsTrue(john.Vehicles.Count == 1, "Invlid Vehicle count for John");
      Assert.IsTrue(jane.Vehicles.Count == 1, "Invlid Vehicle count for Jane");
      

      session = _app.OpenSession();
      john = session.GetEntity<IDriver>(johnId);
      Assert.AreEqual(1, john.Vehicles.Count, "Invalid # of John's cars after transfer.");
      jane = session.GetEntity<IDriver>(janeId);
      Assert.AreEqual(1, jane.Vehicles.Count, "Invalid # of Jane's cars after transfer.");
      veh2 = jane.Vehicles[0];
      Assert.AreEqual(jane, veh2.Owner, "Invalid car2 owner after transfer.");
      Assert.IsNull(veh2.Driver, "Car2 driver expected to be NULL");

      //Check that entity reference is returned initially as record stub
      session = _app.OpenSession();
      veh1 = session.GetEntity<IVehicle>(car1Id);
      var car1driver = veh1.Driver;
      var rec = EntityHelper.GetRecord(car1driver);
      Assert.AreEqual(EntityStatus.Stub, rec.Status, "Entity reference is not loaded as stub.");

      //Test [PropagateUpdatedOn] attribute. We have this attribute on IVehicle.Owner. Whenever we update a vehicle, 
      // it's owner's UpdatedOn value should change as if the owner entity was updated. 
      session = _app.OpenSession();
      jane = session.GetEntity<IDriver>(jane.Id);
      var janeUpdatedOn = jane.UpdatedOn;
      Thread.Sleep(200); //let clock tick
      veh1 = jane.Vehicles[0];
      veh1.Year--; //this should cause new value of jane.UpdatedOn
      session.SaveChanges();
      var janeUpdatedOn2 = jane.UpdatedOn;
      Assert.IsTrue(janeUpdatedOn2 > janeUpdatedOn.AddMilliseconds(50), "PropagateUpdatedOn: expected UpdatedOn of parent entity to change.");

      //Bug fix: GetEntity with Stub option and invalid pk value - should throw error
      session = _app.OpenSession();
      TestUtil.ExpectFailWith<Exception>(() => {
        var ent = session.GetEntity<IVehicle>("abc", LoadFlags.Stub);
      });

      // Investigating reported bug
      var noInstr = session.EntitySet<IDriver>().Where(d => d.Instructor == null).ToList();
      Assert.IsTrue(noInstr.Count > 0, "expected some drivers");

      
    }//method

    // The framework automatically copies some component model attribute (Description, DisplayName, Browsable)
    // for entity interfaces to generated classes. We test here this copying process.
    [TestMethod]
    public void TestStandardAttributes() {
      DeleteAll();
      var session = _app.OpenSession();
      var car = session.NewEntity<IVehicle>(); 
      var modelProp = car.GetType().GetProperty("Model");
      var attrs = modelProp.GetCustomAttributes(true).ToList();
      Assert.IsTrue(attrs.Count > 0, "No standard attributes found on Car.Model property in the generated class.");
      Assert.IsNotNull(attrs.FirstOrDefault(a => a is System.ComponentModel.DisplayNameAttribute), "DisplayNameAttribute not found in the generated class.");
      Assert.IsNotNull(attrs.FirstOrDefault(a => a is System.ComponentModel.DescriptionAttribute), "DescriptionAttribute not found in the generated class.");
      Assert.IsNotNull(attrs.FirstOrDefault(a => a is System.ComponentModel.BrowsableAttribute), "BrowsableAttribute not found in the generated class.");
      Assert.IsNotNull(attrs.FirstOrDefault(a => a is System.ComponentModel.CategoryAttribute), "CategoryAttribute not found in the generated class.");
      //Check EntityMemberInfo properties
      var vehEntInfo = _app.Model.GetEntityInfo(typeof(IVehicle));
      Assert.AreEqual("Represents vehicle entity.", vehEntInfo.Description, "Expected entity description.");
      var modelMember = vehEntInfo.GetMember("Model");
      Assert.AreEqual("Vehicle model", modelMember.DisplayName, "Expected member display name: Vehicle model.");
      Assert.AreEqual("Model of the vehicle.", modelMember.Description, "Invalid member description.");

    }

    [TestMethod]
    public void TestMultipleReadWrites() {
      var timeService = _app.TimeService; 
      //temporarily disable for SQL CE, takes too long for unknown reason and fails
      if (Startup.ServerType == DbServerType.SqlCe)
        return; 
      DeleteAll();
      //We test here that multiple sessions/selects don't bring down the system - that connections are opened/closed appropriately 
      // and connection pool is not exausted.
      var session = _app.OpenSession();

      var jack = session.NewDriver("D005", "Jack", "Smith");
      var johnId = jack.Id;
      session.SaveChanges(); 

      int Count = 200;
      //GC.Collect();
      //Thread.Sleep(100);

      var start = timeService.ElapsedMilliseconds;
      //writes
      for (int i = 0; i < Count; i++) {
        session = _app.OpenSession();
        jack = session.GetEntity<IDriver>(johnId);
        jack.FirstName = "John_" + i;
        session.SaveChanges();
      }
      var ticks = timeService.ElapsedMilliseconds - start; // Expected: MS SQL - 1600ms, SQL CE - 450 ms

      //SQL CE: normal execution time is around 450 ms; but sometimes SQL CE works slower, 10 times slower.
      // the test runs at 6500 ms instead of 450ms
      // TODO: investigate SQL CE slow behavior
      int Timeout = 5000; //5 sec for my laptop
      switch(Startup.ServerType) {
        case DbServerType.Sqlite:
          Timeout = 10000; //10 sec, these 2 are a bit slower
          break;
      }      
      Assert.IsTrue(ticks < Timeout, "Too much time for multiple reaq/writes test, ms: " + ticks); 
      System.Diagnostics.Debug.WriteLine("\r\nTestMultipleReadWrites: ticks = " + ticks + "\r\n");
    }

    [TestMethod]
    public void TestUniqueKey() {
      // Unique key test ----------------------------------------------------------------------------------------
      IEntitySession session;
      object indexAlias, memberNames;
      DataAccessException dex;
      var servType = Startup.ServerType;

      // Try 2 cases
      // 1. Submit 2 drivers with the same license in one update
      session = _app.OpenSession();

      //var dr1 = session.NewDriver("X001", "Jessy", "Jones");
      //var dr2 = session.NewDriver("X001", "JessyJr", "Jones");
      var dr1 = session.NewDriver("X001", "Jessy", "Jones");
      var dr2 = session.NewDriver("X001", "Jim", "Jackson");

      dex = TestUtil.ExpectDataAccessException(() => { session.SaveChanges(); });
      Assert.AreEqual(DataAccessException.SubTypeUniqueIndexViolation, dex.SubType);
      switch(servType) {
        case DbServerType.Sqlite:
          //SQLite is a special case
          var columnNames = dex.Data[DataAccessException.KeyDbColumnNames];
          Assert.AreEqual("Driver.LicenseNumber", columnNames, "SQLite: Unexpected column name(s) in Unique index violation.");
          break; 
        default:
          indexAlias = dex.Data[DataAccessException.KeyIndexAlias];
          memberNames = dex.Data[DataAccessException.KeyMemberNames];
          Assert.AreEqual("LicenseNumber", indexAlias, "Unexpected key name in Unique index violation.");
          Assert.AreEqual("LicenseNumber", memberNames, "Unexpected member name in Unique index violation.");
          break; 
      }

      // 2. Submit driver with license matching already existing with the same license in one update
      session = _app.OpenSession();
      var dr3 = session.NewDriver("M001", "Mindy", "Stone");
      session.SaveChanges();
      var dr4 = session.NewDriver("M001", "Molly", "Sands");
      dex = TestUtil.ExpectDataAccessException(() => { session.SaveChanges(); });
      Assert.AreEqual(DataAccessException.SubTypeUniqueIndexViolation, dex.SubType);
      switch(servType) {
        case DbServerType.Sqlite:
          //SQLite is a special case
          var columnNames = dex.Data[DataAccessException.KeyDbColumnNames];
          Assert.AreEqual("Driver.LicenseNumber", columnNames, "SQLite: Unexpected column name(s) in Unique index violation.");
          break;
        default:
          indexAlias = dex.Data[DataAccessException.KeyIndexAlias];
          memberNames = dex.Data[DataAccessException.KeyMemberNames];
          Assert.AreEqual("LicenseNumber", indexAlias, "Unexpected key name in Unique index violation.");
          Assert.AreEqual("LicenseNumber", memberNames, "Unexpected member name in Unique index violation.");
          break;
      }



    }

    [TestMethod]
    //Tests NotifyPropertyChanged and computed property
    public void TestComputedAndNotifyPropertyChanged() {
      var session = _app.OpenSession();
      var dr1 = session.NewDriver("Z001", "First", "Last");
      // INotifyPropertyChanged and DependsOn test ----------------------------------------------------------------
      // FullName has DependsOn attribute with FirstName, LastName as targets. 
      // So when we change the FirstName, we get two events fired -
      // first for FirstName, then for FullName
      var iNpc = dr1 as INotifyPropertyChanged; 
      var changedProps = new List<string>();
      iNpc.PropertyChanged += (ent, args) => { changedProps.Add(args.PropertyName); };
      dr1.FirstName = "NewFirst";
      //We should have 3 properties in the stack - FistName, FullName, LastFirst
      Assert.AreEqual(3, changedProps.Count, "Invalid number of properties in events stack.");
      Assert.IsTrue(changedProps.Contains("FirstName"), "Expected property changed on FirstName");
      Assert.IsTrue(changedProps.Contains("FullName"), "Expected property changed on FullName");
      Assert.IsTrue(changedProps.Contains("LastFirst"), "Expected property changed on LastFirst");
      //Try cancel changes
      changedProps.Clear();
      session.CancelChanges();
      Assert.IsTrue(changedProps.Count > 0, "Expected PropertyChanged fired on CancelChanges.");

      //Computed persistent prop; verify that column (LicenseHash) is updated in database after we update other prop (LicenseNumber) it dependent on
      var dr2 = session.NewDriver("Z002", "John", "Dow");
      var oldHash = dr2.LicenseHash;
      session.SaveChanges();
      //Change it
      dr2.LicenseNumber = "Z002X";
      session.SaveChanges(); 
      //get hash without loading entity
      var hashObj = session.EntitySet<IDriver>().Where(d => d.Id == dr2.Id).Select(d => new { Hash = d.LicenseHash }).Single(); 
      var newHash = hashObj.Hash;
      Assert.AreNotEqual(oldHash, newHash, "Expected different hash.");
      var expectedHash = Util.StableHash("Z002X");
      Assert.AreEqual(expectedHash, newHash, "Invalid new hash");
    }
    
  }//class
}
