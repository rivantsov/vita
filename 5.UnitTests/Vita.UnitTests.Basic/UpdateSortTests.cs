using System;
using System.ComponentModel;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.UnitTests.Common;

namespace Vita.UnitTests.Basic {

  // Tests for automatic sorting of insert/update/deletes in complex cases, when it is not possible to use
  // ordering by entity type - topological ordering of entities. VITA automatically detects the case
  // and tries to order individual records to satisfy integrity constraints. 
  // We define a specific model with entities referencing each other in a loop, and self-referencing entities
  [Entity]
  public interface IEmployee{
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    string Name { get; set; }
    [Nullable]
    IDepartment Department { get; set; }
    string JobTitle { get; set; }
    [Nullable]
    IEmployee ReportsTo { get; set; }
  }

  [Entity]
  public interface IDepartment {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Size(30)]
    string Name { get; set; }

    IEmployee Manager { get; set; }
  }

  // We skip defining custom entity module and use base EntityModule class
  public class UpdateSortEntityApp : EntityApp {
    public UpdateSortEntityApp() {
      var area = AddArea("updsort");
      var mainModule = new EntityModule(area, "MainModule");
      mainModule.RegisterEntities(typeof(IEmployee), typeof(IDepartment));
    }
  }//class

  public static class UpdateExtensions {
    public static IEmployee NewEmployee(this IEntitySession session, string name, string jobTitle, IEmployee reportsTo = null, IDepartment department = null) {
      var emp = session.NewEntity<IEmployee>();
      emp.Name = name;
      emp.JobTitle = jobTitle;
      emp.ReportsTo = reportsTo;
      emp.Department = department;
      return emp; 
    }
    public static IDepartment NewDepartment(this IEntitySession session, string name, IEmployee manager) {
      var dep = session.NewEntity<IDepartment>();
      dep.Name = name;
      dep.Manager = manager;
      return dep; 
    }
  }

  [TestClass]
  public class UpdateSortTests {
    UpdateSortEntityApp _app;

    [TestInitialize]
    public void Init() {
      _app = new UpdateSortEntityApp();
      SetupHelper.DropSchemaObjects("usort");
      SetupHelper.ActivateApp(_app);
    }

    [TestCleanup]
    public void TestCleanup() {
      if(_app != null)
        _app.Flush();
    }

    
    [TestMethod]
    public void TestUpdateSort() {
      var session = _app.OpenSession();
      //Create root employee - nothing else can be created initially
      var steve = session.NewEmployee("Steve", "CEO");
      session.SaveChanges();
      //create departments 
      var depExec = session.NewDepartment("Executive", steve);
      steve.Department = depExec;
      var liza = session.NewEmployee("Liza", "VP-HR");
      var depHr = session.NewDepartment("HR", liza);
      var john = session.NewEmployee("John", "HR consultant", liza, depHr);
      var jack = session.NewEmployee("Jack", "Sales associate");
      var depSales = session.NewDepartment("Sales", jack);
      var emily = session.NewEmployee("Emily", "VP-IT");
      var depIT = session.NewDepartment("IT", emily);
      var linda = session.NewEmployee("Linda", "Support engineer", emily, depIT);
      RandomizeRecordOrder(session); 
      session.SaveChanges(); 

      // The following does not work - we create Dep and Empl referencing each other. For SQL Server there's no way to find proper order in this case
      // Postgres on the other hand supports so called Deferred Integrity Check - ref integrity is checked at trans commit, not on each record insert.
      // So that would work for Postgres with deferred integrity check.
      // BTW, this feature is part SQL-92 standard. MS - when?! it's 2016!!!! 
      // TODO: test Postgres deferred integrity check
      session = _app.OpenSession();
      var phil = session.NewEmployee("Phil", "Security Head");
      var secDep = session.NewDepartment("Security", phil);
      phil.Department = secDep;
      var circEx = TestUtil.ExpectClientFault(() => { session.SaveChanges(); });
      Assert.AreEqual(ClientFaultCodes.CircularEntityReference, circEx.Faults[0].Code);
    }//method

    private void RandomizeRecordOrder(IEntitySession session) {
      var entSession = session as EntitySession;
      var recs = entSession.RecordsChanged;
      var rand = new Random();
      for (int i = 0; i < recs.Count; i++) {
        var rec = recs[0];
        recs.RemoveAt(0);
        var newIndex = rand.Next(recs.Count - 1);
        recs.Insert(newIndex, rec); 
      }
    }

  }
}
