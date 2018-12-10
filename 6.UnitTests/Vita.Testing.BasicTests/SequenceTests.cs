using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Data.Linq;

namespace Vita.Testing.BasicTests.Sequences {

  [Entity]
  public interface ICar {
    [PrimaryKey, Sequence("IdSequence")]
    long Id { get; set; }
    [Size(30)]
    string Model { get; set; }
    IPerson Owner { get; set; }
    [Nullable]
    int AltId { get; set; }
  }

  [Entity]
  public interface IPerson {
    [PrimaryKey, Sequence("IdSequence")]
    long Id { get; set; }
    [Size(30)]
    string Name { get; set; }
  }

  public class SequenceTestModule : EntityModule {
    public const string IdSequence = "IdSequence";

    public SequenceTestModule(EntityArea area)  : base(area, "SequenceTestModule") {
      RegisterEntities(typeof(ICar), typeof(IPerson));
      this.RegisterSequence(IdSequence, typeof(long));
    }

  }//class

  // We skip defining custom entity module and use base EntityModule class
  public class SequenceTestApp : EntityApp {
    public SequenceTestModule MainModule; 
    public SequenceTestApp() {
      var area = AddArea("seq");
      MainModule = new SequenceTestModule(area);
    }
  }//class


  [TestClass]
  public class SequenceTest {
    SequenceTestApp _app;

    [TestCleanup]
    public void TestCleanup() {
      if(_app != null)
        _app.Flush();
    }


    [TestMethod]
    public void TestSequences() {
      //Only MS SQL and Postgres support sequences
      switch (Startup.ServerType) {
        case Data.Driver.DbServerType.MsSql: case Data.Driver.DbServerType.Postgres:
          break;
        default: return; 
      }
      _app = new SequenceTestApp();
      Startup.ActivateApp(_app);
      var session = _app.OpenSession();

      var valueOne = session.Select(() => 1);
      Assert.AreEqual(1, valueOne);

      var idSeq = _app.Model.FindSequence("IdSequence");
      for(int i = 0; i < 3; i++) {
       var seqValue = session.GetSequenceNextValue(idSeq);
      }

      //using it in SQL query
      var seqQueryList = session.EntitySet<IPerson>().Select(p => new { p.Name, AltId = idSeq.NextValue() }).ToList();

      var john = session.NewEntity<IPerson>();
      john.Name = "Jack M";
      var car1 = session.NewEntity<ICar>();
      car1.Model = "Beatle";
      car1.Owner = john;
      var car2 = session.NewEntity<ICar>();
      car2.Model = "Legacy";
      car2.Owner = john;
      //Check that Identity values immediately changed to actual positive values loaded from database
      Assert.IsTrue(john.Id > 0, "Invalid person ID - expected positive value.");
      Assert.IsTrue(car1.Id > 0, "Invalid car1 ID - expected positive value.");
      Assert.IsTrue(car2.Id > 0, "Invalid car2 ID - expected positive value."); 
      session.SaveChanges();

      //Start new session, load all and check that IDs and relationships are correct
      session = _app.OpenSession();
      var persons = session.GetEntities<IPerson>().ToList();
      john = persons[0];
      var cars = session.GetEntities<ICar>().ToList();
      car1 = cars[0];
      car2 = cars[1];
      Assert.IsTrue(john.Id > 0 && car1.Id > 0 && car2.Id > 0, "IDs expected to become > 0 after saving.");
      Assert.IsTrue(car1.Owner == john, "Owner expected to be John");
      Assert.IsTrue(car2.Owner == john, "Owner expected to be John");
    }//method


  } //class
}// ns
