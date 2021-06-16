using System;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Data.Driver;
using Vita.Tools.Testing;
using System.Runtime.InteropServices;

namespace Vita.Testing.BasicTests.Misc {
  //Description attr is defined in 2 places, resolving ambiguity. The other def in in Microsoft.VisualStudio.TestTools
  using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;


  [TestClass]
  public class MiscTests {
    // making _app static so it's initialized once for all tests;
    //  for console run with multiple servers, remember ServerType for the app, so we create new
    //  when server type changes
    static EntityApp _app;
    static DbServerType _appServerType; 

    [TestInitialize]
    public void Init() {
      if (_app == null || _appServerType != Startup.ServerType) {
        _app = new MiscTestsEntityApp();
        Startup.ActivateApp(_app);
        _appServerType = Startup.ServerType;
      }
    }

    [TestCleanup]
    public void TestCleanup() {
      if (_app != null)
        _app.Flush();
    }

    public void DeleteAll() {
      Startup.DeleteAll(_app);
    }

    [TestMethod]
    public void TestMisc_EntityReferences() {
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
      Assert.AreEqual(1, john.Vehicles.Count, "Invlid Vehicle count for John");
      Assert.AreEqual(1, jane.Vehicles.Count, "Invlid Vehicle count for Jane");


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
      if (Startup.ServerType == DbServerType.MySql) {
        // MySQL fails without this wait. Looks like we need to give some time to DB engine to settle the update. any ideas?!  
        Thread.Sleep(200);
        Thread.Sleep(200);
        Thread.Sleep(200);
        veh1 = session.GetEntity<IVehicle>(car1Id);
        // without pause above, it loads NULL into FK driver_id
        var drId = EntityHelper.GetProperty(veh1, "Driver_Id");
        Assert.IsTrue(drId != null, "MySql odd behavior:  Driver_Id is null in loaded record.");
      } else {
        veh1 = session.GetEntity<IVehicle>(car1Id);
      }
      // now the test itself, verify that veh1.Driver is a stub (empty record with just FK values)
      var car1driver = veh1.Driver;
      var rec = EntityHelper.GetRecord(car1driver);
      Assert.AreEqual(EntityStatus.Stub, rec.Status, "Entity reference is not loaded as stub.");

      //Test [PropagateUpdatedOn] attribute. We have this attribute on IVehicle.Owner. Whenever we update a vehicle, 
      // it's owner's UpdatedOn value should change as if the owner entity was updated. 
      _app.TimeService.SetCurrentOffset(TimeSpan.FromSeconds(5)); //push time forward
      session = _app.OpenSession();
      jane = session.GetEntity<IDriver>(jane.Id);
      var janeUpdatedOn = jane.UpdatedOn;
      // Thread.Sleep(200); //let clock tick
      veh1 = jane.Vehicles[0];
      veh1.Year--; //this should cause new value of jane.UpdatedOn
      session.SaveChanges();
      var janeUpdatedOn2 = jane.UpdatedOn;
      Assert.IsTrue(janeUpdatedOn2 > janeUpdatedOn.AddMilliseconds(50), "PropagateUpdatedOn: expected UpdatedOn of parent entity to change.");
      _app.TimeService.SetCurrentOffset(TimeSpan.Zero);

      //Bug fix: GetEntity with Stub option and invalid pk value - should throw error
      session = _app.OpenSession();
      TestUtil.ExpectFailWith<Exception>(() => {
        var ent = session.GetEntity<IVehicle>("abc", LoadFlags.Stub);
      });

      // Investigating reported bug
      var noInstr = session.EntitySet<IDriver>().Where(d => d.Instructor == null).ToList();
      Assert.IsTrue(noInstr.Count > 0, "expected some drivers");


    }//method

    // The framework automatically copies some component model attributes (Description, DisplayName, Browsable)
    // for entity interfaces to generated classes. We test here this copying process.
    [TestMethod]
    public void TestMisc_StandardAttributes() {
      DeleteAll();
      var session = _app.OpenSession();
      var car = session.NewEntity<IVehicle>();
      var modelProp = car.GetType().GetProperty("Model");
      var memberAttrs = modelProp.GetCustomAttributes(true).ToList();
      Assert.IsTrue(memberAttrs.Count > 0, "No standard attributes found on Car.Model property in the generated class.");
      Assert.IsNotNull(memberAttrs.FirstOrDefault(a => a is DisplayNameAttribute), "DisplayNameAttribute not found in the generated class.");
      Assert.IsNotNull(memberAttrs.FirstOrDefault(a => a is DescriptionAttribute), "DescriptionAttribute not found in the generated class.");
      Assert.IsNotNull(memberAttrs.FirstOrDefault(a => a is BrowsableAttribute), "BrowsableAttribute not found in the generated class.");
      Assert.IsNotNull(memberAttrs.FirstOrDefault(a => a is CategoryAttribute), "CategoryAttribute not found in the generated class.");
      //check one value
      var descrAttr = memberAttrs.OfType<DescriptionAttribute>().FirstOrDefault();
      Assert.AreEqual("Model of the vehicle.", descrAttr.Description, "IVehicle.Model description does not match.");
    }

    [TestMethod]
    public void TestMics_MultipleReadWrites() {
      var timeService = _app.TimeService;
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
      var timeMs = timeService.ElapsedMilliseconds - start; // Expected: MS SQL - 1600ms, SQL CE - 450 ms

      int Timeout = 5000; //5 sec for my laptop is OK
      switch (Startup.ServerType) {
        case DbServerType.SQLite:
          Timeout = 10000; //10 sec, SQLite runs a bit slower
          break;
      }
      Assert.IsTrue(timeMs < Timeout, "Too much time for multiple reaq/writes test, ms: " + timeMs);
      System.Diagnostics.Trace.WriteLine("\r\nTestMultipleReadWrites: time = " + timeMs + " ms \r\n");
    }

    [TestMethod]
    public void TestMics_UniqueKey() {
      // Unique key test ----------------------------------------------------------------------------------------
      IEntitySession session;
      object indexAlias, memberNames;
      DataAccessException dex;
      var servType = Startup.ServerType;

      // Try 2 cases
      // 1. Submit 2 drivers with the same license in one update
      session = _app.OpenSession();

      var dr1 = session.NewDriver("X001", "Jessy", "Jones");
      var dr2 = session.NewDriver("X001", "Jim", "Jackson");

      dex = TestUtil.ExpectDataAccessException(() => { session.SaveChanges(); });
      Assert.AreEqual(DataAccessException.SubTypeUniqueIndexViolation, dex.SubType);
      switch (servType) {
        case DbServerType.SQLite:
          //SQLite is a special case; we setup the flag to attach schema to tablename (misc_XXX)
          var columnNames = dex.Data[DataAccessException.KeyDbColumnNames];
          Assert.AreEqual("misc_Driver.LicenseNumber", columnNames, "SQLite: Unexpected column name(s) in Unique index violation.");
          break;
        default:
          indexAlias = dex.Data[DataAccessException.KeyIndexAlias];
          memberNames = dex.Data[DataAccessException.KeyMemberNames];
          Assert.AreEqual("LicenseNumber", indexAlias, "Unexpected key alias in Unique index violation.");
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
      switch (servType) {
        case DbServerType.SQLite:
          //SQLite is a special case
          var columnNames = dex.Data[DataAccessException.KeyDbColumnNames];
          Assert.AreEqual("misc_Driver.LicenseNumber", columnNames, "SQLite: Unexpected column name(s) in Unique index violation.");
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
    public void TestMics_Computed_NotifyPropertyChanged() {
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
      //Change LicenseNumber, computed/persisted column LicenseHash will be updated automatically
      dr2.LicenseNumber = "Z002X";
      session.SaveChanges();
      //get hash directly without loading entity
      var hashInDb = session.EntitySet<IDriver>().Where(d => d.Id == dr2.Id).Select(d => d.LicenseHash).Single();
      Assert.AreNotEqual(oldHash, hashInDb, "Expected different hash.");
      var expectedHash = "Z002X".GetHashCode();
      Assert.AreEqual(expectedHash, hashInDb, "Invalid new hash");
    }



    [TestMethod]
    public void TestMics_DeleteMany() {
      DeleteAll();
      IEntitySession session;

      // create 10 drivers with the same license in one update
      session = _app.OpenSession();
      var drivers = new List<IDriver>();
      for (int i = 0; i < 10; i++) {
        drivers.Add(session.NewDriver("L" + i, "F" + i, "L" + i));
      }
      session.SaveChanges();
      // Using parameter
      // now delete all - should be one command
      foreach (var dr in drivers)
        session.DeleteEntity(dr);
      session.SaveChanges();
      var cmd = session.GetLastCommand();
      // make sure it is delete-many, in one statement
      if (Startup.ServerType == DbServerType.Postgres)
        Assert.IsTrue(cmd.CommandText.Contains(" ANY("), "Expected delete command with Any(...) clause");
      else
        Assert.IsTrue(cmd.CommandText.Contains(" IN "), "Expected delete command with IN clause");
    }


    [TestMethod]
    public void TestMisc_MatchBy2Refs() {
      // test for a bug - matching properties of 2 references to the same table (v.Owner and v.Driver); 
      DeleteAll();
      var session = _app.OpenSession();
      var john = session.NewDriver("D001", "John", "Dow");
      var jane = session.NewDriver("D002", "Jane", "Crane");
      var veh = session.NewVehicle("Ford", 2000, john, jane);

      var veh2 = session.NewVehicle("Nissan", 2000, jane, john);
      var veh3 = session.NewVehicle("Chevy", 2000, john, null);
      session.SaveChanges();

      var session2 = _app.OpenSession();
      var qry = session2.EntitySet<IVehicle>().Where(v => v.Owner.FirstName == "Jane" && v.Driver.FirstName == "John");
      var ford = qry.FirstOrDefault();
      // var cmd = session2.GetLastCommand();
      // Debug.WriteLine(cmd.CommandText);
      Assert.AreEqual("Jane", ford.Owner.FirstName, "2-ref test: owner name does not match");
      Assert.AreEqual("John", ford.Driver.FirstName, "2-ref test: driver name does not match");
    }


    [TestMethod]
    public void TestMisc_DbComputedMembers() {
      // test for a bug - matching properties of 2 references to the same table (v.Owner and v.Driver); 
      DeleteAll();
      var session = _app.OpenSession();
      var john = session.NewDriver("D001", "John", "Dow");
      var jane = session.NewDriver("D002", "Jane", "Crane");
      session.SaveChanges();

      session = _app.OpenSession();
      var allDrivers = session.EntitySet<IDriver>().ToList();
      // check lic expires prop
      var future4y = DateTime.Now.AddYears(4); 
      foreach(var d in allDrivers) {
        Assert.IsTrue(d.LicenseExpiresOn_NoCol > future4y);
      }

      // get drivers with lic numbers and exp dates; DbAddYears is implemented as SQL;
      // the c# function DbAddYears is registered with the module (it's class thru module.RegisterSqlFunctions)
      //  s
      var qry = session.EntitySet<IDriver>()
                      .Select(d => new {
                        Name = d.FirstName,
                        LicNum = d.LicenseNumber,
                        LicDatePlus3 = d.LicenseIssuedOn.DbAddYears(3)
                      });
      var drivers = qry.ToList();
      Assert.AreEqual(2, drivers.Count, "Expected 2 drivers.");
      var nowPlus2 = DateTime.Now.AddYears(2);
      var allAfter2y = drivers.All(d => d.LicDatePlus3 > nowPlus2);
      Assert.IsTrue(allAfter2y, "Expected all dates in the future.");

    }
  }//class
}
