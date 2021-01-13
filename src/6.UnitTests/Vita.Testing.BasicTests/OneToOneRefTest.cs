using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;

namespace Vita.Testing.BasicTests.OneToOne {

  // Testing discovered bug in LINQ with one-to-one relationship
  [Entity]
  public interface IDocHeader {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Size(Sizes.Name)]
    string Name { get; set; }
    [OneToOne]
    IDocDetails Details { get; }
    [OneToOne]
    IDocDetails2 Details2 { get; } //we never create IDocDetails, to test null ref here
  }

  [Entity]
  public interface IDocDetails {
    [PrimaryKey]
    IDocHeader Header { get; set; } //one-to-one relation
    [Size(Sizes.Description)]
    string Details { get; set; }
  }

  [Entity]
  public interface IDocDetails2 {
    [PrimaryKey]
    IDocHeader Header { get; set; } //one-to-one relation
    [Size(Sizes.Description)]
    string Details2 { get; set; }
  }


  [Entity]
  public interface IDocComments {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    IDocDetails Details { get; set; }
    [Size(Sizes.Description)]
    string Comment { get; set; }
  }



  // We skip defining custom entity module and use base EntityModule class
  public class OneToOneEntityApp : EntityApp {
    public OneToOneEntityApp() {
      var area = AddArea("one");
      var mainModule = new EntityModule(area, "MainModule");
      mainModule.RegisterEntities(typeof(IDocHeader), typeof(IDocDetails), typeof(IDocDetails2), typeof(IDocComments));
    }
  }//class


  [TestClass]
  public class OneToOneRefTest {
    EntityApp _app;

    [TestCleanup]
    public void TestCleanup() {
      if(_app != null)
        _app.Flush();
    }


    [TestMethod]
    public void TestOneToOneRef() {
      _app = new OneToOneEntityApp();
      Startup.ActivateApp(_app);
      var session = _app.OpenSession();

      //initial setup
      session = _app.OpenSession();
      var header = session.NewEntity<IDocHeader>();
      header.Name = "Doc1";
      var det = session.NewEntity<IDocDetails>();
      det.Details = "Some details";
      det.Header = header;
      var cm = session.NewEntity<IDocComments>();
      cm.Details = det;
      cm.Comment = "Some comment";
      session.SaveChanges();
      //test LINQ queries - used to fail
      var query = session.EntitySet<IDocComments>().Where(dc => dc.Details == det);
      var cmList = query.ToList();
      Assert.IsTrue(cmList.Count == 1, "Expected 1 comment.");
      //Test can delete method, also used to fail with one-to-one
      Type[] bt;
      var canDel = session.CanDeleteEntity(det, out bt);
      Assert.IsFalse(canDel, "Expected CanDelete = false.");

      // test [OneToOne] entity
      // IDocHeader.Details is 'back-ref' property, marked with OneToOne attribute
      // IDocHeader.Details2 is similar, but we did not create IDocDetails2 record, so it should be null
      var docId = header.Id; 
      session = _app.OpenSession();
      var doc = session.GetEntity<IDocHeader>(docId);
      var det1 = doc.Details;
      Assert.IsNotNull(det1, "Expected Details");
      var det2 = doc.Details2;
      Assert.IsNull(det2, "Expected Details2 = null");
      //do it again, to check it is correctly cached in record (so it does not blow up)
      det1 = doc.Details;
      det2 = doc.Details2;
      // test using [OneToOne] references in Linq
      session = _app.OpenSession();
      var q2 = session.EntitySet<IDocHeader>().Where(h => h.Details.Details == "Some details");
      var docs = q2.ToList();
      Assert.AreEqual(1, docs.Count, "Expected Doc header");


    }//method

  }//class
}
