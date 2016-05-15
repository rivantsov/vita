using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Authorization;
using Vita.Entities.Runtime;
using Vita.Samples.BookStore;

namespace Vita.UnitTests.Extended {


  [TestClass]
  public class AuthorizationTests {
    IAuthorizationService _authorizationService; 

    [TestInitialize]
    public void TestInit() {
      SetupHelper.InitApp();
      _authorizationService = SetupHelper.BooksApp.GetService<IAuthorizationService>(); 
    }
    [TestCleanup]
    public void TearDown() {
      SetupHelper.TearDown();
    }

    [TestMethod]
    public void TestAuthorization() {
      // First let's open non-secure session, load Users and lookup ID's of some existing objects - we will use them in tests
      var app = SetupHelper.BooksApp;
      var nonSecureSession = app.OpenSystemSession();
      // Preparation - find specific entities (books, users, etc)
      var allUsers = nonSecureSession.GetEntities<IUser>(take: 20);
      var allBooks = nonSecureSession.GetEntities<IBook>(take: 10);
      var allAuthors = nonSecureSession.GetEntities<IAuthor>(take: 10);
      var allPubs = nonSecureSession.GetEntities<IPublisher>(take: 10);
      var allOrders = nonSecureSession.GetEntities<IBookOrder>(take: 10);
      nonSecureSession = null; // to never use it after this

      // Get IDs of some entities
      var csBookId = allBooks.First(b => b.Title.StartsWith("c#")).Id;
      var vbBookId = allBooks.First(b => b.Title.StartsWith("VB")).Id;
      var kidBookId = allBooks.First(b => b.Title.StartsWith("Three")).Id; //3 little piggies
      var authorJohnId = allAuthors.First(a => a.FirstName == "John").Id;
      var authorJackId = allAuthors.First(a => a.FirstName == "Jack").Id;
      var msPubId = allPubs.First(p => p.Name.StartsWith("MS")).Id;
      var kidPubId = allPubs.First(p => p.Name.StartsWith("Kid")).Id;
      var oldDiegoOrderId = allOrders.First(ord => ord.User.UserName == "Diego").Id;

      // Create UserInfo objects for different users
      var anon = UserInfo.Anonymous;
      var dora = allUsers.First(u => u.UserName == "Dora").ToUserInfo(); //creates UserInfo from IUser entity
      var diego = allUsers.First(u => u.UserName == "Diego").ToUserInfo();
      var lindaTheEditor = allUsers.First(u => u.UserName == "Linda").ToUserInfo();
      var charlieTheManager = allUsers.First(u => u.UserName == "Charlie").ToUserInfo();
      var jessyCustSupport = allUsers.First(u => u.UserName == "Jessy").ToUserInfo();
      var johnTheAuthor = allUsers.First(u => u.UserName == "John").ToUserInfo();
      var booksAuth = SetupHelper.BooksApp.Authorization;

      #region Anonymous User Role =========================================================================================
      EnsurePasses("Anonymous user can browse books, authors, publishers, reviews.", () => {
        var secureSession = OpenSecureSession(anon);
        secureSession.DemandReadAccessLevel = ReadAccessLevel.Read; //Requires Read permission
        var csBook = secureSession.GetEntity<IBook>(csBookId);
        var csTitle = csBook.Title; //try read Title
        var authorJohn = secureSession.GetEntity<IAuthor>(authorJohnId);
        var johnFullName = authorJohn.FullName; //read IAuthor properties
        var johnBio = authorJohn.Bio;
        var reviews = secureSession.GetEntities<IBookReview>(take: 10);
        string reviewText, reviewBy;
        foreach(var rv in reviews) {
          reviewText = rv.Review; //can access any review
          reviewBy = rv.User.DisplayName; //anon can read display name of any user, to see who posted the review, but nothing else
        }
      });
      // We just checked that anon user can read IUser.DisplayName property for any other user;
      // Let's check that anon cannot see any other IUser columns
      EnsureFails("Anonymous user cannot read UserName (only user's DisplayName)", () => {
        var secureSession = OpenSecureSession(anon);
        var review = secureSession.EntitySet<IBookReview>().First();
        var reviewBy = review.User.UserName; //blows up
      });
      //that's all anonymous user can do. To create reviews or post orders, we need IUser instance, so user must be signed up and logged in.
      #endregion

      #region Customer Role (logged in user) =========================================================================================

      EnsurePasses("Customer can browse books, authors, publishers, reviews.", () => {
        var secureSession = OpenSecureSession(dora);
        secureSession.DemandReadAccessLevel = ReadAccessLevel.Read; //Requires Read permission
        var csBook = secureSession.GetEntity<IBook>(csBookId);
        var csTitle = csBook.Title; //try read Title
        var authorJohn = secureSession.GetEntity<IAuthor>(authorJohnId);
        var johnFullName = authorJohn.FullName; //read IAuthor properties
        var johnBio = authorJohn.Bio;
        var reviews = secureSession.GetEntities<IBookReview>(take: 10);
        string reviewText;
        foreach(var rv in reviews)
          reviewText = rv.Review; //can access any review
      });

      EnsurePasses("Customer can update his/her User record", () => {
        var secureSession = OpenSecureSession(dora);
        var doraEnt = secureSession.GetEntity<IUser>(dora.UserId);
        doraEnt.DisplayName += " the Explorer"; //Dora the Explorer
        secureSession.SaveChanges();//passes
        //let's restore the name
        doraEnt.DisplayName = "Dora";
        secureSession.SaveChanges();
      });

      EnsureFails("Customer cannot read other user's User record except DisplayName", () => {
        var secureSession = OpenSecureSession(dora);
        var diegoRec = secureSession.GetEntity<IUser>(diego.UserId);
        var diegoUserName = diegoRec.UserName; // blows up
      });

      var doraReview1Id = Guid.Empty; //local vars to keep IDs across lambdas
      var doraReview2Id = Guid.Empty;
      EnsurePasses("Customer can write reviews", () => {
        var secureSession = OpenSecureSession(dora);
        var csBook = secureSession.GetEntity<IBook>(csBookId);
        var vbBook = secureSession.GetEntity<IBook>(vbBookId);
        var iDora = secureSession.GetEntity<IUser>(dora.UserId, LoadFlags.Stub); // stub is enough here
        var review1 = secureSession.NewReview(iDora, csBook, 5, "Excellent!", "Recommend to everyone!");
        var review2 = secureSession.NewReview(iDora, vbBook, 4, "Very good.", "Must read for VB programmers");
        secureSession.SaveChanges();
        doraReview1Id = review1.Id; //save it in a local var, wil use it in the next test
        doraReview2Id = review2.Id;
      });

      EnsureFails("Customer cannot update other users' reviews.", () => {
        var secureSession = OpenSecureSession(diego);
        var doraReview = secureSession.GetEntity<IBookReview>(doraReview1Id);
        doraReview.Review = " Not so good... "; // blows up
      });

      EnsureFails("Customer cannot create a review linked to other user.", () => {
        var secureSession = OpenSecureSession(diego);
        var csBook = secureSession.GetEntity<IBook>(csBookId);
        var iDora = secureSession.GetEntity<IUser>(dora.UserId, LoadFlags.Stub); // stub is enough here
        var doraReview = secureSession.NewReview(iDora, csBook, 3, "Dora likes it", "I know Dora likes it. Diego");
        secureSession.SaveChanges(); //should blow up here
      });

      EnsureFails("Customer cannot delete other users' reviews.", () => {
        var secureSession = OpenSecureSession(diego);
        var doraReview = secureSession.GetEntity<IBookReview>(doraReview1Id);
        secureSession.DeleteEntity(doraReview);
      });

      EnsurePasses("Customer can update, delete his/her reviews.", () => {
        //Update
        var secureSession = OpenSecureSession(dora);
        var doraReview = secureSession.GetEntity<IBookReview>(doraReview1Id);
        doraReview.Review += " (update: some more info)";
        secureSession.SaveChanges();
        //Delete
        secureSession = OpenSecureSession(dora);
        doraReview = secureSession.GetEntity<IBookReview>(doraReview1Id);
        secureSession.DeleteEntity(doraReview); // we test that we can call this method
        // secureSession.SaveChanges(); do not actually delete
      });


      Guid newDoraOrderId = Guid.Empty;
      EnsurePasses("Customer can buy books, use coupons", () => {
        var secureSession = OpenSecureSession(dora);
        var iDora = secureSession.GetEntity<IUser>(dora.UserId, LoadFlags.Stub); // stub is enough here
        var doraOrder = secureSession.NewOrder(iDora);
        var csBook = secureSession.GetEntity<IBook>(csBookId);
        doraOrder.Add(csBook, 1);
        doraOrder.CompleteOrder("C2"); // C2 is coupon code; this will lookup coupon entity
        secureSession.SaveChanges();
        newDoraOrderId = doraOrder.Id; //save it in a local var, will use it later
      });

      // Customer can Peek coupons (order-creation code looks up coupon by code), but cannot Read (see) them.
      EnsureFails("Customer cannot READ coupon entities.", () => {
        var secureSession = OpenSecureSession(dora);
        secureSession.DemandReadAccessLevel = ReadAccessLevel.Read; // Means that property reads are for showing stuff to the User 
        var coupon = secureSession.GetEntities<ICoupon>(take: 10).First(); //That will work, because Customer has Peek permission
        var promoCode = coupon.PromoCode;  //should blow up
      });
      EnsureFails("Customer cannot update coupon entities, except AppliedOn property.", () => {
        var secureSession = OpenSecureSession(dora);
        var coupon = secureSession.EntitySet<ICoupon>().Where(c => c.PromoCode == "C2").First(); //this works with PEEK permission
        coupon.PromoCode = "C2x"; //should blow up
      });

      EnsureFails("Customer cannot see other users' orders", () => {
        var secureSession = OpenSecureSession(diego);
        var doraOrder = secureSession.GetEntity<IBookOrder>(newDoraOrderId); //should blow up
      });

      EnsureFails("Customer cannot see other users' orders (LINQ)", () => {
        var secureSession = OpenSecureSession(diego);
        var doraOrders = secureSession.EntitySet<IBookOrder>().Where(o => o.User.Id == dora.UserId).ToList(); ///should blow up here
      });

      EnsureFails("Customer cannot see other users' orders (LINQ, FirstOrDefault)", () => {
        var secureSession = OpenSecureSession(diego);
        var doraOrder = secureSession.EntitySet<IBookOrder>().Where(o => o.User.Id == dora.UserId).FirstOrDefault();
      });

      EnsureFails("Customer cannot see other users' orders (LINQ, auto types)", () => {
        var secureSession = OpenSecureSession(diego);
        // for auto types, auth system does not check the object or list returned. But entities inside (if any) do have authorization enabled. 
        // So the query below does not blow up; it will fail later, when we tried to read the value from the order inside.
        var doraOrderInfo = secureSession.EntitySet<IBookOrder>().Where(o => o.User.Id == dora.UserId)
                                           .Select(o => new { Order = o, Total = o.Total }).First();
        var total = doraOrderInfo.Order.Total; // should blow up here
      });


      EnsureFails("Customer cannot delete other users' orders", () => {
        var secureSession = OpenSecureSession(diego);
        //TODO: review returning RecordStubs later; might need better authorization enforcement
        // Comment: we do a trick here - we load a record Stub - an empty record with PrimaryKey initialized.  
        // System does not check access rights for stubs initially, to avoid forcing unnecessary record load. 
        // But it will check permissions as soon as we try to access record values, or delete the record
        var doraOrder = secureSession.GetEntity<IBookOrder>(newDoraOrderId, LoadFlags.Stub); //does not fail yet
        secureSession.DeleteEntity(doraOrder); // blows up
      });

      EnsureFails("Customer cannot update books", () => {
        var secureSession = OpenSecureSession(dora);
        var csBook = secureSession.GetEntity<IBook>(csBookId);
        csBook.Price += 1; //should fail here
      });

      EnsureFails("Customer cannot delete books", () => {
        var secureSession = OpenSecureSession(dora);
        var csBook = secureSession.GetEntity<IBook>(csBookId);
        secureSession.DeleteEntity(csBook); //blows here
      });

      EnsureFails("Customer cannot update Authors", () => {
        var secureSession = OpenSecureSession(dora);
        var authorJohn = secureSession.GetEntity<IAuthor>(authorJohnId);
        authorJohn.Bio = "Spam spam spam"; // blows up
      });

      EnsureFails("Customer cannot update Publishers", () => {
        var secureSession = OpenSecureSession(dora);
        var msPub = secureSession.GetEntity<IPublisher>(msPubId);
        msPub.Name = "Microsoft Publishing"; //fails here
      });
      #endregion

      #region Author Role =============================================================
      EnsurePasses("Author can edit his Bio and his book description in scope of AuthorEditGrant.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        using(booksAuth.AuthorEditGrant.Execute(secureSession.Context)) {
          var john = secureSession.GetEntity<IAuthor>(authorJohnId);
          john.Bio = "Some new Bio";
          var csBook = secureSession.GetEntity<IBook>(csBookId);
          csBook.Description = "New description";
          secureSession.SaveChanges();
        }
      });
      EnsureFails("Author cannot edit his LastName in scope of AuthorEditGrant.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        using(booksAuth.AuthorEditGrant.Execute(secureSession.Context)) {
          var john = secureSession.GetEntity<IAuthor>(authorJohnId);
          john.LastName = "Sharpp";
        }
      });
      EnsureFails("Author cannot edit his book Title even in scope of AuthorEditGrant.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        using(booksAuth.AuthorEditGrant.Execute(secureSession.Context)) {
          var csBook = secureSession.GetEntity<IBook>(csBookId);
          csBook.Title = "c# programming for experts";
        }
      });
      EnsureFails("Author cannot change his book Price even in scope of AuthorEditGrant.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        using(booksAuth.AuthorEditGrant.Execute(secureSession.Context)) {
          var csBook = secureSession.GetEntity<IBook>(csBookId);
          csBook.Price += 10;
        }
      });
      EnsureFails("Author cannot edit his Bio outside AuthorEditGrant.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        var john = secureSession.GetEntity<IAuthor>(authorJohnId);
        john.Bio = "Some another Bio";
      });
      EnsureFails("Author cannot edit his book description outside AuthorEditGrant.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        var csBook = secureSession.GetEntity<IBook>(csBookId);
        csBook.Description = "New description";
      });
      EnsureFails("Author cannot edit other author's Bio even in scope of AuthorEditGrant.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        using(booksAuth.AuthorEditGrant.Execute(secureSession.Context)) {
          var jack = secureSession.GetEntity<IAuthor>(authorJackId);
          jack.Bio = "Corrected Bio";
        }
      });
      EnsureFails("Author cannot edit a random book Description even in scope of AuthorEditGrant.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        using(booksAuth.AuthorEditGrant.Execute(secureSession.Context)) {
          var kidBook = secureSession.GetEntity<IBook>(kidBookId);
          kidBook.Description = "New description";
        }
      });
      EnsureFails("Author cannot access any orders.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        var doraOrder = secureSession.GetEntity<IBookOrder>(newDoraOrderId);
      });
      EnsurePasses("Author can read Publishers.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        var pub = secureSession.GetEntity<IPublisher>(msPubId);
        var pubName = pub.Name;
        pub = secureSession.GetEntity<IPublisher>(kidPubId);
        pubName = pub.Name;
      });
      EnsureFails("Author cannot update publishers.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        var pub = secureSession.GetEntity<IPublisher>(msPubId);
        pub.Name = "Microsoft Publishing";
      });
      EnsureFails("Author cannot create publishers.", () => {
        var secureSession = OpenSecureSession(johnTheAuthor);
        var pub = secureSession.NewEntity<IPublisher>();
      });
      #endregion

      #region BookEditor Role =============================================================
      EnsurePasses("Book editor can create/update/delete authors, books.", () => {
        var secureSession = OpenSecureSession(lindaTheEditor);
        var kidPub = secureSession.GetEntity<IPublisher>(kidPubId);
        var mtwain = secureSession.NewAuthor("Mark", "Twain");
        var mtwainBook = secureSession.NewBook(BookEdition.Paperback, BookCategory.Fiction,
            "Tom Soyer", "Tom Soyer adventures.", kidPub, DateTime.Now.AddYears(-1).Date, 10);
        mtwainBook.Authors.Add(mtwain);
        secureSession.SaveChanges();
        // now let's delete it; note that IBookAuthor link is deleted automatically through CascadeDelete
        // attribute on IBookAuthor.Book property. 
        secureSession.DeleteEntity(mtwainBook);
        secureSession.DeleteEntity(mtwain);
        secureSession.SaveChanges();
      });
      EnsureFails("Book editor cannot create publishers.", () => {
        var secureSession = OpenSecureSession(lindaTheEditor);
        var pub = secureSession.NewEntity<IPublisher>();
      });
      EnsureFails("Book editor cannot update publishers.", () => {
        var secureSession = OpenSecureSession(lindaTheEditor);
        var msPub = secureSession.GetEntity<IPublisher>(msPubId);
        msPub.Name = "Microsoft Publishing";
      });
      EnsureFails("Book editor cannot delete reviews.", () => {
        var secureSession = OpenSecureSession(lindaTheEditor);
        var doraReview = secureSession.GetEntity<IBookReview>(doraReview2Id);
        secureSession.DeleteEntity(doraReview);
      });

      #endregion

      #region CustomerSupport Role =============================================================
      EnsurePasses("CustomerSupport can view customer users.", () => {
        var secureSession = OpenSecureSession(jessyCustSupport);
        // CustomerSupport can view Users, but only those that are Customers or Authors
        // system should automatically inject filter. Filters for Customer and Authors are added separately in authorization rules,
        // and runtime automatically combines the 2 conditions. Here we test this combinining filters feature.
        var allSupportedUsers = secureSession.EntitySet<IUser>().ToList();
        /*   SQL: 
            SELECT "Id", "UserName", "UserNameHash", "DisplayName", "Type", "IsActive"
            FROM "books"."User"
            WHERE ("Type" = 1 OR "Type" = 2)          
         */
        Assert.IsTrue(allSupportedUsers.Count > 0, "CustomerSupport: expected some users.");
        var allAreCustOrAuthor = allSupportedUsers.All(u => u.Type == UserType.Customer || u.Type == UserType.Author);
        var someAreCustomers = allSupportedUsers.Any(u => u.Type == UserType.Customer);
        var someAreAuthors = allSupportedUsers.Any(u => u.Type == UserType.Author);
        Assert.IsTrue(allAreCustOrAuthor, "Expected customers and authors only.");
        Assert.IsTrue(someAreCustomers, "Expected some customers.");
        Assert.IsTrue(someAreAuthors, "Expected some authors.");
      });
      #endregion

      #region StoreManager Role =============================================================
      EnsurePasses("StoreManager can create/delete publishers.", () => {
        var secureSession = OpenSecureSession(charlieTheManager);
        var pub = secureSession.NewPublisher("SciFi Publishing");
        secureSession.SaveChanges();
        secureSession.DeleteEntity(pub);
        secureSession.SaveChanges();
      });
      EnsurePasses("StoreManager can delete reviews.", () => {
        var secureSession = OpenSecureSession(charlieTheManager);
        var doraReview = secureSession.GetEntity<IBookReview>(doraReview2Id);
        secureSession.DeleteEntity(doraReview);
        secureSession.SaveChanges();
      });
      EnsurePasses("StoreManager can create/delete coupons.", () => {
        var secureSession = OpenSecureSession(charlieTheManager);
        var coupon = secureSession.NewCoupon("S01", 10, DateTime.Now.AddMonths(3));
        secureSession.SaveChanges();
        secureSession.DeleteEntity(coupon);
        secureSession.SaveChanges();
      });
      EnsurePasses("StoreManager can adjust orders within ManagerAdjustOrderGrant.", () => {
        var secureSession = OpenSecureSession(charlieTheManager);
        using(booksAuth.ManagerAdjustOrderGrant.Execute(secureSession.Context, null, newDoraOrderId)) {
          var doraOrder = secureSession.GetEntity<IBookOrder>(newDoraOrderId);
          // Verify that we can modify IBookOrder and IBookOrderLine entities
          doraOrder.Lines[0].Price -= 2; // make a discount on first line
          doraOrder.Total -= 2; // change total
          secureSession.SaveChanges();
        }
      });
      EnsureFails("StoreManager can adjust ONLY the order for which the ManagerAdjustOrderGrant started.", () => {
        var secureSession = OpenSecureSession(charlieTheManager);
        //We activate the grant for doraOrderId, but will try to modify diego's order inside - this should fail
        using(booksAuth.ManagerAdjustOrderGrant.Execute(secureSession.Context, null, newDoraOrderId)) {
          var diegoOrder = secureSession.GetEntity<IBookOrder>(oldDiegoOrderId);
          diegoOrder.Total += -2; // should blow up
        }
      });
      #endregion

      #region Retrieving access rights without actually accessing the data
      // For retrieving rights at table level, use ISecureSession.IsActionAllowed<TEntity>(action) method. 
      // To retrieve rights for particular entity and property, use EntityHelper.GetEntityAccess helper method to get the descriptor, 
      // and then use the descriptor to get the detailed info.
      {
        //Check that book editor can edit books, but cannot edit publishers
        var secureSession = OpenSecureSession(lindaTheEditor);
        Assert.IsTrue(secureSession.IsAccessAllowed<IBook>(AccessType.CRUD),
                        "Invalid access descriptor: editor can create, update, delete books.");
        Assert.IsFalse(secureSession.IsAccessAllowed<IPublisher>(AccessType.UpdateStrict), "Invalid access descriptor: editor cannot update publishers.");
        // Check that author can edit book Abstract, but not Title (within AuthorEditGrant)
        secureSession = OpenSecureSession(johnTheAuthor);
        using(booksAuth.AuthorEditGrant.Execute(secureSession.Context)) {
          var csBook = secureSession.GetEntity<IBook>(csBookId);
          var csBookAccess = EntityHelper.GetEntityAccess(csBook);
          Assert.IsTrue(csBookAccess.CanUpdate<IBook>(bk => bk.Abstract), "Failed access check: author can update book abstract.");
          // the same with different overload
          Assert.IsTrue(csBookAccess.CanUpdate("Abstract"), "Failed access check: author can update book abstract.");
          // should not be able to update title
          Assert.IsFalse(csBookAccess.CanUpdate<IBook>(bk => bk.Title), "Failed access check: author cannot update the title.");
        }
      }
      #endregion

    }//method



    // Helpers and Utilities

    private void EnsurePasses(string testStep, Action action) {
      try {
        action.Invoke();
      } catch (AuthorizationException authExc) {
        Debug.WriteLine("Test step: " + testStep);
        //Report user's rights to entity from AuthorizationException
        Debug.WriteLine("AuthorizationException Summary:\r\n" + authExc.Summary);
        throw;
      } catch (Exception) {
        Debug.WriteLine("Test step: " + testStep);
        throw; 
      }
    }

    //User is used for reporting permissons to Debug output, in case anticipated exception is not thrown
    private void EnsureFails(string testStep, Action action) {
      try {
        action.Invoke();
        //if invoke did not fail, throw. 
        throw new Exception("Anticipated exception was not thrown. Test: " + testStep);
      } catch (AuthorizationException) {
        return; // it is expected, so suppress it
      } 
    }

    private ISecureSession OpenSecureSession(UserInfo user) {
      var opContext = new OperationContext(SetupHelper.BooksApp, user);
      var session = opContext.OpenSecureSession();
      return session; 
    }

  }//class
}
