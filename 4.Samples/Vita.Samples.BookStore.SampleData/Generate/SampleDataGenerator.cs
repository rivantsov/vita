using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Vita.Entities;
using Vita.Modules.Login;
using System.Reflection;

namespace Vita.Samples.BookStore.SampleData {

  //Generates sample data used in unit tests. Normally this file should be in a separate assembly related to unit tests

  public static class SampleDataGenerator {
    public const string DefaultPassword = "TestPass34%"; 

    public static void CreateUnitTestData(EntityApp app) {
      CreateBasicTestData(app);
      CreateSampleBooks(app);
      // CreateScheduledEvents(app);
    }

    public static void CreateBasicTestData(EntityApp app) {
      var session = app.OpenSystemSession();
      session.EnableCache(false);
      Vita.Modules.Login.LoginModule.ImportDefaultSecretQuestions(session);
      session.SaveChanges();
      //Create users
      var stan = session.CreateUser("Stan", UserType.Customer);
      var linda = session.CreateUser("Linda", UserType.BookEditor);
      var jessy = session.CreateUser("Jessy", UserType.CustomerSupport);
      var charlie = session.CreateUser("Charlie", UserType.StoreAdmin);
      var kevin = session.CreateUser("Kevin", UserType.StoreAdmin);
      //customers
      var dora = session.CreateUser("Dora", UserType.Customer, email: "dora@email.com");
      var diego = session.CreateUser("Diego", UserType.Customer);
      var duffy = session.CreateUser("Duffy", UserType.Customer);
      var ferb = session.CreateUser("Ferb", UserType.Customer);
      var cartman = session.CreateUser("Cartman", UserType.Customer, email: "cartman@email.com");
      session.SaveChanges();
      // Setup secret questions for dora
      var doraLogin = session.EntitySet<ILogin>().First(lg => lg.UserName == "dora"); //logins are lower-case, important for Postgres
      SetupSampleSecretQuestions(doraLogin);
      session.SaveChanges(); 
    }

    public static void CreateSampleBooks(EntityApp app) {
      //Create identity for sample data generator; this results in SampleDataGenerator showing up in UserSession/UserTransaction tables
      // Books and coupons will reference these transactions as 'CreatedIn'
      var session = app.OpenSystemSession();
      var dataGenUser = session.NewUser("SampleDataGenerator", UserType.StoreAdmin);
      session.SaveChanges();
      var userInfo = new UserInfo(dataGenUser.Id, dataGenUser.UserName);
      var dataGenOpCtx = new OperationContext(app, userInfo);
      session = dataGenOpCtx.OpenSession();
      session.EnableCache(false); 

      //Publishers and authors
      var msPub = session.NewPublisher("MS Books"); //we are using extension method here
      var kidPub = session.NewPublisher("Kids Books");
      var johnBio = ConstructLongText(4000);
      var authorJohn = session.NewAuthor("John", "Sharp", johnBio);
      var authorJack = session.NewAuthor("Jack", "Pound");
      var authorJim = session.NewAuthor("Jim", "Hacker"); //this author is not user - we'll use this author to check some tricky query in tests
      var john = authorJohn.User = session.CreateUser("John", UserType.Author);

      var pubDate = DateTime.Today.AddYears(-1);
      //Books on programming from MS Books
      var csBook = session.NewBook(BookEdition.Paperback | BookEdition.EBook, BookCategory.Programming,
                 "c# Programming", "Expert programming in c#", msPub, pubDate, 20.0m);
      // Some multiline text in Abstract
      csBook.Abstract = @"Expert guide to programming in c# 4.0. 
Highly recommended for beginners and experts.
Covers c# 4.0.";
      csBook.CoverImage = LoadImageFromResource(session, "csBookCover.jpg");
      csBook.Authors.Add(authorJohn); //this is many-to-many
      csBook.Authors.Add(authorJack);
      csBook.Editor = session.EntitySet<IUser>().First(u => u.UserName == "Linda");
      var vbBook = session.NewBook(BookEdition.Paperback | BookEdition.Hardcover, BookCategory.Programming,
                          "VB Programming", "Expert programming in VB", msPub, pubDate, 25.0m);
      vbBook.Authors.Add(authorJack);
      vbBook.CoverImage = LoadImageFromResource(session, "vbBookCover.jpg");

      //Folk tale, no authors
      var kidBook = session.NewBook(BookEdition.Hardcover, BookCategory.Kids,
           "Three little pigs", "Folk tale", kidPub, pubDate, 10.0m);
      var winBook = session.NewBook(BookEdition.Hardcover, BookCategory.Programming,
           "Windows Programming", "Introduction to Windows Programming", msPub, pubDate.AddYears(-10), 30.0m);
      winBook.Authors.Add(authorJohn);
      winBook.CoverImage = LoadImageFromResource(session, "winBookCover.jpg");
      var comicBook = session.NewBook(BookEdition.Paperback, BookCategory.Fiction, "IronMan", null, kidPub, null, 3);
      //Coupons
      var coupon1 = session.NewCoupon("C1", 10, DateTime.Now.AddMonths(1));
      var coupon2 = session.NewCoupon("C2", 10, DateTime.Now.AddMonths(1));
      var coupon3 = session.NewCoupon("C3", 10, DateTime.Now.AddMonths(1));

      try {
        session.SaveChanges();  //Save books, coupons, users and logins
      } catch(ClientFaultException ex) {
        var msgs = ex.GetMessages();
        Debug.WriteLine(msgs);
        throw;
      }

      //Orders
      var dora = session.EntitySet<IUser>().First(u => u.UserName == "Dora");
      var doraOrder = session.NewOrder(dora);
      doraOrder.Add(csBook, 1);
      doraOrder.Add(kidBook, 2);
      doraOrder.CompleteOrder("C1");
      //Create one empty order, for testing includes in queries
      var doraOrder2 = session.NewOrder(dora);
      doraOrder2.Status = OrderStatus.Canceled; 

      var diego = session.EntitySet<IUser>().First(u => u.UserName == "Diego");
      var diegoOrder = session.NewOrder(diego);
      diegoOrder.Add(vbBook, 1);
      diegoOrder.Add(csBook, 1);
      diegoOrder.Add(winBook, 1);
      diegoOrder.CompleteOrder();
      //Reviews
      var doraReview = session.NewReview(dora, csBook, 5, "Very interesting book!", "Liked it very much!");
      var diegoReview = session.NewReview(diego, vbBook, 1, "Not worth it.", "Did not like it at all.");
      // special reviews with text including wildcards for LIKE operator - will use them to test wildcard escaping in LIKE
      var duffy = session.EntitySet<IUser>().First(u => u.UserName == "Duffy");
      session.NewReview(duffy, comicBook, 1, "'Boo", "'Boo");
      session.NewReview(duffy, comicBook, 1, "_Boo", "_Boo");
      session.NewReview(duffy, comicBook, 1, "%Boo", "%Boo");
      session.NewReview(duffy, comicBook, 1, "[Boo]", "[Boo]");
      session.NewReview(duffy, comicBook, 1, "]Boo[", "]Boo[");
      session.NewReview(duffy, comicBook, 1, @"\Boo\oo", @"\Boo\oo");
      session.NewReview(duffy, comicBook, 1, @"/Boo/oo", @"/Boo/oo");

      //Save orders
      try {
        session.SaveChanges();  
      } catch (ClientFaultException ex) {
        var msgs = ex.GetMessages(); 
        Debug.WriteLine(msgs);
        throw;
      }
    }

    private static IUser CreateUser(this IEntitySession session, string userName, UserType userType, string password = DefaultPassword, string email = null) {
      var user = session.NewUser(userName, userType, userName);
      var loginMgr = session.Context.App.GetService<ILoginManagementService>();
      var login = loginMgr.NewLogin(session, userName, password, userId: user.Id, loginId: user.Id);
      if (!string.IsNullOrEmpty(email))
        loginMgr.AddFactor(login, ExtraFactorTypes.Email, email);
      return user;
    }

    private static void SetupSampleSecretQuestions(ILogin login) {
      var session = EntityHelper.GetSession(login); 
      var loginMgr = session.Context.App.GetService<ILoginManagementService>(); 
      var allQuestions = session.GetEntities<ISecretQuestion>();
      var qFriend = allQuestions.First(q => q.Question.Contains("friend"));
      var qFood = allQuestions.First(q => q.Question.Contains("favorite food"));
      var qColor = allQuestions.First(q => q.Question.Contains("favorite color"));
      loginMgr.AddSecretQuestionAnswer(login, 1, qFriend, "Diego");
      loginMgr.AddSecretQuestionAnswer(login, 2, qFood, "banana");
      loginMgr.AddSecretQuestionAnswer(login, 3, qColor, "yellow");
    }


    private static string ConstructLongText(int length) {
      const string loremIpsum = @"
Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. 
Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. 
Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
      int numCopies = length / loremIpsum.Length + 1;
      var sb = new StringBuilder(length);
      for (int i = 0; i < numCopies; i++)
        sb.Append(loremIpsum);
      return sb.ToString().Substring(0, length);
    }


    private static IImage LoadImageFromResource(IEntitySession session, string name, 
                ImageType type = ImageType.BookCover, string mediaType = "image/jpeg") {
      const int MaxImageSize = 100 * 1024;
      var fileName = "Vita.Samples.BookStore.SampleData.Generate.Images." + name;
      var thisAsm = typeof(SampleDataGenerator).GetTypeInfo().Assembly;
      var stream = thisAsm.GetManifestResourceStream(fileName);
      var reader = new System.IO.BinaryReader(stream);
      var bytes = reader.ReadBytes(MaxImageSize);
      var image = session.NewImage(name, type, mediaType, bytes);
      return image;
    }

  }//class
}//ns
