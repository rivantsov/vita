using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Tools;
using Vita.Data;

namespace Vita.Testing.BasicTests.Identity {
  // Using both int and long as identity columns, just to test proper type conversions
  [Entity]
  public interface ICar {
    [PrimaryKey, Identity]
    long Id { get; }
    [Size(30)]
    string Model { get; set; }
    IPerson Owner { get; set; }
    // Testing a bug for non-batch mode, when ref prop is null (and target has identity PK)
    [Nullable]
    IPerson CoOwner { get; set; }
  }

  [Entity]
  public interface IPerson {
    [PrimaryKey, Identity]
    int Id { get; }
    [Size(30)]
    string Name { get; set; }
  }

  // ITreeNode is used in a test with tree-like structure (self-ref entity type)
  [Entity]
  public interface ITreeNode {
    [PrimaryKey, Identity]
    int Id { get; set; }

    [Nullable]
    ITreeNode Parent { get; set; }

    [Size(30)]
    string Name { get; set; }

  }

  // We skip defining custom entity module and use base EntityModule class
  public class IdentityTestsEntityApp : EntityApp {
    public IdentityTestsEntityApp() {
      var area = AddArea("ident");
      var mainModule = new EntityModule(area, "MainModule");
      mainModule.RegisterEntities(typeof(ICar), typeof(IPerson), typeof(ITreeNode));
    }
  }//class


  [TestClass]
  public class IdentityTests {
    EntityApp _app;

    [TestCleanup]
    public void TestCleanup() {
      if (_app != null)
        _app.Flush();
    }

    [TestMethod]
    public void TestIdentityColumns() {
      // test with and without batch mode
      TestIdentityColumns(false);
      TestIdentityColumns(true); 
    }

    public void TestIdentityColumns(bool batchMode) {
      _app = new IdentityTestsEntityApp();
      Startup.ActivateApp(_app);
      //if(Startup.ServerType == Data.Driver.DbServerType.SQLite)
      DeleteAllData();
      // We create session this way to set the batch mode flag
      var ctx = new OperationContext(_app); 
      IEntitySession session = new EntitySession(ctx, 
        options: batchMode ? EntitySessionOptions.None : EntitySessionOptions.DisableBatchMode);
      var john = session.NewEntity<IPerson>();
      john.Name = "John S";
      //john.Name = "Джон Ш"; //Russian
      var car1 = session.NewEntity<ICar>();
      car1.Model = "Beatle";
      car1.Owner = john;
      car1.CoOwner = null; 
      var car2 = session.NewEntity<ICar>();
      car2.Model = "Explorer";
      car2.Owner = john;
      Assert.IsTrue(car1.Id < 0, "Car ID is expected to be <0 before saving."); 
      session.SaveChanges();
      //Check that Identity values immediately changed to actual positive values loaded from database
      Assert.IsTrue(john.Id > 0, "Invalid person ID - expected positive value.");
      Assert.IsTrue(car1.Id > 0, "Invalid car1 ID - expected positive value.");
      Assert.IsTrue(car2.Id > 0, "Invalid car2 ID - expected positive value."); 

      //Start new session, load all and check that IDs and relationships are correct
      session = _app.OpenSession();
      john = session.EntitySet<IPerson>().Where(p => p.Name == "John S").Single();
      var cars = session.GetEntities<ICar>().ToList();
      car1 = cars[0];
      car2 = cars[1];
      Assert.IsTrue(john.Id > 0 && car1.Id > 0 && car2.Id > 0, "IDs expected to become > 0 after saving.");
      Assert.IsTrue(car1.Owner == john, "Owner expected to be John");
      Assert.IsTrue(car2.Owner == john, "Owner expected to be John");

      // Testing list.Contains with long (bigint) values, used to fail for SQL Server
      var longArray = new long[] { long.MaxValue - 10, long.MaxValue - 9, long.MaxValue - 8 };
      // we just test that this does not blow up
      var someCars = session.EntitySet<ICar>().Where(c => longArray.Contains(c.Id)).ToList();
      var sql = session.GetLastCommand().CommandText;
      Assert.AreEqual(0, someCars.Count);

    }//method

    [TestMethod]
    public void TestIdentityInLargeBatch() {
      _app = new IdentityTestsEntityApp();
      Startup.ActivateApp(_app);
      // For SQLite objects are not dropped (FK problems), so we need to instead delete all data
      if(Startup.ServerType == Data.Driver.DbServerType.SQLite)
        DeleteAllData();
      var sqlDialect = _app.GetDefaultDatabase().DbModel.Driver.SqlDialect;
      var saveParamCount = sqlDialect.MaxParamCount; 
      sqlDialect.MaxParamCount = 20; //to cause update batch split into multiple commands
      var session = _app.OpenSession();
      // add 50 owners, then 200 cars with random owners
      // our goal is to create update set with inserts of linked entities with identity pk/fk
      // we test how identity values are carried between commands (from IPerson.Id to ICar.OwnerId)
      var owners = new List<IPerson>(); 
      var rand = new Random();
      for (int i = 0; i < 50; i++) {
        var owner = session.NewEntity<IPerson>();
        owner.Name = "Owner" + i;
        owners.Add(owner);
      }
      for (int i = 0; i < 100; i++) {
        var car = session.NewEntity<ICar>();
        car.Model = "Model" + i;
        car.Owner = owners[rand.Next(owners.Count)];
      }
      try {
        session.SaveChanges(); //we just test that it succeeds
      } finally {
        //revert max param count back to normal - to avoid disturbing other tests
        sqlDialect.MaxParamCount = saveParamCount;
      }
    }

    // test for fix for reported bug: https://github.com/rivantsov/vita/issues/104
    //   the SaveChanges error described was discovered with Postgres; but it happens
    //   with other servers when batch mode is off. 
    //   Posgres cannot do batch mode when inserting identity records, 
    //   (it does not support output parameters in dynamic SQL), while MS SQL can use 
    //    batch mode. So Postgres is switching to no-batch update and failed. Now fixecd
    [TestMethod]
    public void TestIdentityWithSelfRefEntities() {
      var saveOptions = Startup.DbOptions; 
      try {
        // Run twice, 
        // Batch mode
        Startup.DbOptions |= DbOptions.UseBatchMode;
        RunTestIdentityWithSelfRefEntities();
        // No-batch mode
        Startup.DbOptions &= ~DbOptions.UseBatchMode;
        RunTestIdentityWithSelfRefEntities();
      } finally {
        Startup.DbOptions = saveOptions; 
      }
    }

    public void RunTestIdentityWithSelfRefEntities() {
      _app = new IdentityTestsEntityApp();
      Startup.ActivateApp(_app);
      //if(Startup.ServerType == Data.Driver.DbServerType.SQLite)
      DeleteAllData();
      var session = _app.OpenSession();

      // Create 3-level tree and save it
      var node1 = session.NewNode("N1");

      // level 2
      var node11 = session.NewNode("N11", node1);
      var node12 = session.NewNode("N12", node1);
      var node13 = session.NewNode("N13", node1);

      // level 3 
      var node111 = session.NewNode("N1111", node11);
      var node112 = session.NewNode("N1112", node11);
      var node121 = session.NewNode("N1121", node12);
      var node122 = session.NewNode("N1122", node12);
      var node131 = session.NewNode("N1131", node13);
      var node132 = session.NewNode("N1132", node13);

      DataUtility.RandomizeRecordOrder(session);
      session.SaveChanges();

      // test passed if it executed without exceptions
      Assert.IsTrue(true, "never happens");
    }

    // For SQLite objects are not dropped (FK problems), so we need to instead delete all data
    // to make sure we start with empty tables
    private void DeleteAllData() {
      var session = _app.OpenSession();
      var allCars = session.GetEntities<ICar>(take: 100).ToList();
      foreach(var c in allCars)
        session.DeleteEntity(c);
      session.SaveChanges();
      var allP = session.GetEntities<IPerson>(take: 100).ToList();
      foreach(var p in allP)
        session.DeleteEntity(p);
      session.SaveChanges();
    }

  }

  static class IdentityHelper {
    public static ITreeNode NewNode(this IEntitySession session, string name, ITreeNode parent = null) {
      var node = session.NewEntity<ITreeNode>();
      node.Name = name;
      node.Parent = parent;
      return node; 
    }
  }

}
