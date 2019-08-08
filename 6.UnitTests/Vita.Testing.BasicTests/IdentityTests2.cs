using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Entities.Runtime;

namespace Vita.Testing.BasicTests.IdentityRef {

  // Special identity tests
  // Special case - composite PK, with FK to parent (which is identity) and another identity

  [Entity]
  public interface IUser {
    [PrimaryKey, Identity]
    int Id { get; set; }
    [Size(30)]
    string Name { get; set; }
  }

  [Entity, PrimaryKey("User,OrderId")]
  public interface IUserPost {
    IUser User { get; set; }
    int OrderId { get; set; }
    [Size(100)]
    string Text { get; set; }
  }

  // We skip defining custom entity module and use base EntityModule class
  public class IdentityRefTestsEntityApp : EntityApp {
    public IdentityRefTestsEntityApp() {
      var area = AddArea("ident2");
      var mainModule = new EntityModule(area, "MainModule");
      mainModule.RegisterEntities(typeof(IUser), typeof(IUserPost));
    }
  }//class


  [TestClass]
  public class IdentityRefTests {
    EntityApp _app;

    [TestCleanup]
    public void TestCleanup() {
      if (_app != null)
        _app.Flush();
    }

    [TestMethod]
    public void TestIdentityWithCompositePK() {
      // Only MsSql and Postgres allow this (composite PK with identity column inside); the other servers reject it.
      switch(Startup.ServerType) {
        case Data.Driver.DbServerType.MsSql:
        case Data.Driver.DbServerType.Postgres:
          break;
        default:
          return; 
      }
      // test with and without batch mode
      RunTest(false);
      RunTest(true);
    }

    public void RunTest(bool batchMode) {
      _app = new IdentityRefTestsEntityApp();
      Startup.ActivateApp(_app);
      //if(Startup.ServerType == Data.Driver.DbServerType.SQLite)
      DeleteAllData();
      // We create session this way to set the batch mode flag
      var ctx = new OperationContext(_app); 
      IEntitySession session = new EntitySession(ctx, 
        options: batchMode ? EntitySessionOptions.None : EntitySessionOptions.DisableBatchMode);

      var john = session.NewUser("john");
      var post1 = john.NewPost("john post 1", 1);
      var post2 = john.NewPost("john post 2", 2);
      var post3 = john.NewPost("john post 3", 3);
      var ben = session.NewUser("ben");
      var post1b = ben.NewPost("ben post 1", 1);
      var post2b = ben.NewPost("ben post 2", 2);
      var post3b = ben.NewPost("ben post 3", 3);
      session.SaveChanges();
      //Check that Identity values immediately changed to actual positive values loaded from database
      Assert.IsTrue(post1.OrderId > 0, "Invalid OrderID - expected positive value.");
      Assert.IsTrue(post2.OrderId > 0, "Invalid OrderID - expected positive value.");
      Assert.IsTrue(post3.OrderId > 0, "Invalid OrderID - expected positive value.");

      //Start new session, load all and check that IDs and relationships are correct
      session = _app.OpenSession();
      var posts = session.EntitySet<IUserPost>().ToList();
      foreach (var post in posts)
        Assert.IsTrue(post.OrderId > 0);


    }//method

    // For SQLite objects are not dropped (FK problems), so we need to instead delete all data
    // to make sure we start with empty tables
    private void DeleteAllData() {
      var session = _app.OpenSession();
      session.ExecuteDelete<IUserPost>(session.EntitySet<IUserPost>());
      session.ExecuteDelete<IUser>(session.EntitySet<IUser>());
    }

  }

  static class Id2Extensions {
    public static IUser NewUser(this IEntitySession session, string name) {
      var user = session.NewEntity<IUser>();
      user.Name = name;
      return user;
    }
    public static IUserPost NewPost(this IUser user, string text, int orderId) {
      var session = EntityHelper.GetSession(user);
      var post = session.NewEntity<IUserPost>();
      post.User = user;
      post.Text = text;
      post.OrderId = orderId;
      return post; 
    }
  }//IdExtensions class
}
