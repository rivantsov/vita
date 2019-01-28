using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Samples.BookStore;
using Vita.Data.Driver;
using Vita.Modules.Login;
using Vita.Tools.Testing;
using Vita.Data.Sql;

namespace Vita.Testing.ExtendedTests {


  [TestClass]
  public class LinqTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }

    [TestCleanup]
    public void TearDown() {
      Startup.TearDown();
    }



    #region Helper objects for LINQ test
    public const string PublisherNameConst = "c# const string"; // to use in tests, to check how Linq handles references to constants
    // field and prop to test local evaluation in LINQ queries
    private string _someClassField;
    private string SomeClassProp { get { return "boo!"; } }
    public static string SomeStaticClassField;

    //An artificial container for query parameters - to check how LINQ engine handles parameters in the form qp.Field or qp.Prop
    class QueryParamsContainer {
      public string Field;
      public string Prop { get; set; }
    }
    #endregion

    [TestMethod]
    public void TestLinqBasics() {
      var app = Startup.BooksApp;
      IDbCommand lastCmd;

      //Init
      var session = app.OpenSession();
      var books = session.EntitySet<IBook>();
      var authors = session.EntitySet<IAuthor>();
      var users = session.EntitySet<IUser>();
      var pubs = session.EntitySet<IPublisher>();

      var csTitle = "c# Programming";
      var vbTitle = "VB Programming";

      // new + new
      var idTitles = books.Select(b => new { Id1 = b.Id, Title1 = b.Title })
                         .Select(b => new { Id2 = b.Id1, Title2 = b.Title1 }).ToList();
      Assert.IsTrue(idTitles.Count > 0, "Expected Id-Title pairs");

      // book by title
      var qBooksByTitle = from b in books
                          where b.Title == csTitle
                          select b;
      var lstBooksByTitle = qBooksByTitle.ToList();
      Assert.IsTrue(lstBooksByTitle.Count > 0, "Books by title failed.");

      // Books by publisher's name; skip, take test
      var msbooks = from b in books
                    where b.Publisher.Name == "MS Books"
                    orderby b.Title.ToUpper() //ToUpper for Oracle, case-sensitive
                    select b;
      var msBookList = msbooks.ToList();
      Assert.AreEqual(3, msBookList.Count, "Invalid number of MS books.");
      var msbook0 = msBookList[0]; //c# book should be the first book when sorted by title
      Assert.AreEqual("c# Programming", msbook0.Title, "Invalid title of c# book.");
      //Same with skip, take
      var qSkipTake = msbooks.Skip(1).Take(1);
      var lstSkipTake = qSkipTake.ToList();
      Assert.AreEqual(1, lstSkipTake.Count, "Invalid # of books in skip/take.");
      // Just some sanity check, ran into strange behavior here with session.LastCommand
      lastCmd = session.GetLastCommand();
      Assert.IsTrue(lastCmd.CommandText.Contains("'MS Books'"), "Investigate: LastCommand contains wrong command: " + lastCmd.CommandText);

      // Test bitwise op in SQL. Finding books by editions - we use bit-wise operation on book.Editions property - which is a flagset enum
      session = app.OpenSession();
      books = session.EntitySet<IBook>();
      var eBooks = from b in books
                   where (b.Editions & BookEdition.EBook) != 0
                   select b;
      var eBooksList = eBooks.ToList();
      Assert.IsTrue(eBooksList.Count == 1, "Invalid number of e-books");

      // Using == for entity objects in queries
      var progCat = BookCategory.Programming;
      var msPub = session.EntitySet<IPublisher>().Single(p => p.Name == "MS Books");
      var msbooks2 = from b in books
                     where b.Publisher == msPub && b.Category == progCat
                     orderby b.Title
                     select b;
      msBookList = msbooks2.ToList();
      Assert.IsTrue(msBookList.Count > 0, "Query with entity object == comparison failed.");

      //BookAuthors link table
      var bookAuthors = session.EntitySet<IBookAuthor>();
      var aJack = session.EntitySet<IAuthor>().Where(a => a.FirstName == "Jack").First();
      var qBA = from ba in bookAuthors
                where ba.Author == aJack && ba.Book.Title == "c# Programming"
                select ba;
      var lstBAs = qBA.ToList();
      Assert.IsTrue(lstBAs.Count > 0, "Failed to find book-author record by author and book title.");

      // LINQ with FirstOrDefault() - these are executed slightly differently from queries that return result sets
      session = app.OpenSession();
      var msp = (from p in session.EntitySet<IPublisher>()
                 where p.Name == "MS Books"
                 select p).FirstOrDefault();
      Assert.IsTrue(msp != null, "Query with FirstOrDefault failed.");
      Assert.AreEqual("MS Books", msp.Name, "Query with FirstOrDefault failed, invalid name.");
      //Check that entity is attached to session
      var mspRec = EntityHelper.GetRecord(msp);
      Assert.IsTrue(mspRec.Session != null, "FirstOrDefault query failed: the result record is not attached to session.");
      Assert.IsTrue(msp.Books.Count > 0, "Failed to read publisher's books: entity is not attached to session.");

      // Query that returns a derived entity, different from the original 'EntitySet' entity
      session = app.OpenSession();
      //Return publishers of books in Kids category - we have one kids book, so expect one pub record
      var kidPubs = from b in session.EntitySet<IBook>()
                    where b.Category == BookCategory.Kids
                    select b.Publisher;
      var listPubs = kidPubs.ToList();
      Assert.AreEqual(1, listPubs.Count, "Unexpected pub count in special LINQ query.");
      // The same but with FirstOrDefault
      var firstKidPub = (from b in session.EntitySet<IBook>()
                         where b.Category == BookCategory.Kids
                         select b.Publisher).FirstOrDefault();
      Assert.IsNotNull(firstKidPub, "First kid Publisher is null in special LINQ query.");

      // Test FirstOrDefault with null result - special case for EntityCache.CloneEntity method
      session = app.OpenSession();
      var none = session.EntitySet<IBook>().Where(b => b.Title == "123").FirstOrDefault();
      Assert.IsNull(none, "FirstOrDefault with null result failed.");

      // Query returning anonymous type
      // Return pairs book-title, price
      session = app.OpenSession();
      var someId = Guid.Empty;
      var qryBooksAnon = from b in session.EntitySet<IBook>()
                         where b.Category == BookCategory.Programming
                         select new { Id = b.Id.ToString(), Title = b.Title, Price = b.Price }; //testing how ToString() works with Guids - only in select output!
      var listBooksAnon = qryBooksAnon.ToList();
      Assert.IsTrue(listBooksAnon.Count > 0, "Failed to retrieve anon type.");
      Assert.IsTrue(!string.IsNullOrEmpty(listBooksAnon[0].Id), "ToString query translation failed");

      //Same but with parameter in output 
      var someKey = "someKey"; //some value to include into results - testing how such field initialized from parameter is handled
      var qryBooksAnon2 = from b in session.EntitySet<IBook>()
                          where b.Category == BookCategory.Programming
                          select new { b.Title, b.Price, Key = someKey };
      var listBooksAnon2 = qryBooksAnon2.ToList();
      Assert.IsTrue(listBooksAnon2.Count > 0, "Failed to retrieve anon type.");

      // Return pairs of (book, publisher)
      session = app.OpenSession();
      var bpPairs = from b in session.EntitySet<IBook>()
                    where b.Category == BookCategory.Programming
                    select new { Book = b, Publisher = b.Publisher };
      var bpPairsList = bpPairs.ToList();
      Assert.IsTrue(bpPairsList.Count > 0, "Failed to retrieve anon type.");

      // Return anon type with pair of entities, one of them is nullable
      session = app.OpenSession();
      var qAuthorUser = from a in authors
                        select new { Author = a, User = a.User };
      var listAuthorUser = qAuthorUser.ToList();
      Assert.IsTrue(listAuthorUser.Count > 0, "Author-User query failed.");
      // Author Jim Hacker is not a user, so its User prop should be null
      var jimInfo = listAuthorUser.First(au => au.Author.LastName == "Hacker");
      Assert.IsTrue(jimInfo.User == null, "User prop is not null for Author Jim Hacker");

      // Some odd query returning list of constants
      books = session.EntitySet<IBook>();
      var numberQuery = from b in books
                        where b.Publisher.Name == "MS Books"
                        select 1;
      var numbersFromQuery = numberQuery.ToList();
      Assert.IsTrue(numbersFromQuery.Count > 0, "Number query did not return rows");

      // Join thru nullable reference - it should work even with cache!
      var qAuthorsJohn = from a in authors
                         where a.User.UserName == "John"
                         select a;
      var lstAuthorsJohn = qAuthorsJohn.ToList();
      Assert.AreEqual(1, lstAuthorsJohn.Count, "Failed to find author by user name");


      // Aggregate methods
      session = app.OpenSession();
      books = session.EntitySet<IBook>();
      var maxPrice = books.Max(b => b.Price);
      Assert.IsTrue(maxPrice > 0, "Max price is invalid.");
      // Queryable.Average method is special in some way - it is not a generic on result type, but has a number of overloads
      // for specific numeric types. This test checks how fwk handles this
      var avgPrice = books.Average(b => b.Price);
      Assert.IsTrue(avgPrice > 0, "Average price is invalid.");

      // Test 'locally evaluatable' pieces in queries
      session = app.OpenSession();
      pubs = session.EntitySet<IPublisher>();
      var msBooksName = "local var value";
      _someClassField = "field value";
      // NOTE: a bug in PostGres npgsql (version v2.1.3/Sept 2014); in the following query, if we put literal "Literal'string" before 
      //       any expr involving parameter, then provider/postgres fails with error 42703 'Column P0 does not exist'
      //       But if value with quote is after all parameters, everything works fine! To see it, just uncomment the clause below
      var pq = from p in pubs
               where
                        p.Name == msBooksName  // param in query
                                               //|| p.Name != "Postgres'fails"
                     || p.Name == _someClassField //param
                     || p.Name == SomeClassProp   //param
                     || p.Name == PublisherNameConst // const in query, not parameters
                     || p.Name == "Literal'string" //const (literal)
                     || p.Name != "blah"           //const
               select p;
      var pqList = pq.ToList();
      Assert.IsTrue(pqList.Count > 0, "Query with local variables failed.");

      //Using extra container object
      var qparams = new QueryParamsContainer() { Field = csTitle, Prop = vbTitle };
      var qBooksWithParamObj = from b in books
                               where b.Title == qparams.Field || b.Title == qparams.Prop
                               select b;
      var lstBooksWithParamObj = qBooksWithParamObj.ToList();
      Assert.AreEqual(2, lstBooksWithParamObj.Count, "Query with param object failed");

      // Test using predicates comparing with null. It is special case - must be translated to 'IS NULL' in SQL
      var bq = from b in books
               where b.Abstract == null
               select b;
      var booksWithoutAbstract = bq.ToList();
      Assert.IsTrue(booksWithoutAbstract.Count > 0, "Query with predicate checking for null failed.");
      // testing entity reference null check (foreign key != null)
      var qAuthUsers = from a in authors
                       where a.User != null
                       select a;
      var lstAuthUsers = qAuthUsers.ToList();
      Assert.IsTrue(lstAuthUsers.Count > 0, "Query with check for non null failed.");

      // Test using calculations over local values directly into the query. Did not work initially, now fixed.
      // The date value should be calculated before running the query and supplied as parameter. Same with price
      var priceCutOff = 5;
      Func<decimal, decimal> getDiscountedPrice = (decimal p) => p * 0.8m;
      var queryWithCalc = session.EntitySet<IBook>().Where(b => b.PublishedOn > DateTime.Now.AddYears(-3) && b.Price > getDiscountedPrice(priceCutOff));
      var lstFromCalc = queryWithCalc.ToList();
      lastCmd = session.GetLastCommand(); //to check SQL in debugger
      Assert.IsTrue(lstFromCalc.Count > 0, "Query with local calc values failed.");

      // Test for MySql handling of Guid IDs
      // Must use an entity that is not in full entity cache: like IBookOrder, not IBook.
      session = app.OpenSession();
      var someOrder = session.GetEntities<IBookOrder>(take: 10).First();
      var someOrderId = someOrder.Id;
      var qGetOrderById = from bo in session.EntitySet<IBookOrder>()
                          where bo.Id == someOrderId
                          select bo;
      var listOrders = qGetOrderById.ToList();
      Assert.AreEqual(1, listOrders.Count, "Failed to get order by Id.");
      Assert.AreEqual(someOrderId, listOrders[0].Id, "Failed to get order by Id.");

      // Linq query against entity with computed column
      // Testing loading entity with computed column; I've encountered trouble with computed columns in new linq engine, so verifying the fix here.
      // Important - we should use entity that is NOT in full cache, like IBookOrder (it has computed field Summary).
      var orders = session.EntitySet<IBookOrder>();
      var qOrders = from ord in orders
                    where ord.Status == OrderStatus.Completed
                    select ord;
      var lstOrders = qOrders.ToList();
      Assert.IsTrue(lstOrders.Count > 0, "Failed to retrieve orders.");
      var summ0 = lstOrders[0].Summary;
      Assert.IsFalse(string.IsNullOrWhiteSpace(summ0), "Failed to read order summary");

      //Test string concatenation
      var authFullNames = session.EntitySet<IAuthor>().Select(a => a.LastName + ", " + a.FirstName).ToList();
      Assert.IsTrue(authFullNames.Count > 0, "Expected non-empty list.");
      Assert.IsTrue(authFullNames.Contains("Sharp, John"), "Expected john sharp in the list.");
      //Test string Length methods
      var authNameLens = session.EntitySet<IAuthor>().Select(a => a.LastName.Length + a.FirstName.Length).ToList();
      Assert.IsTrue(authNameLens.Count > 0, "Expected non-empty list of name lengths.");
      lastCmd = session.GetLastCommand();

      // decimal linq params
      decimal pr = 5.0m;
      var expensiveBooks = session.EntitySet<IBook>().Where(b => b.Price > pr).ToList();
      lastCmd = session.GetLastCommand();
      Assert.IsTrue(expensiveBooks.Count > 0, "Query with decimal param failed.");

    }

    [TestMethod]
    public void TestLinqWithNew() {
      //test for using new DateTime() in Linq queries; 
      // first with constants
      var session = Startup.BooksApp.OpenSession();
      var dateQ = from bo in session.EntitySet<IBookOrder>()
                  where bo.CreatedOn < new DateTime(2020, 1, 1)
                  select bo;
      var listByDate = dateQ.ToList();
      Assert.IsTrue(listByDate.Count > 1, "Query with new DateTime(const) failed. ");

      // now more complex case
      var year = 2020;
      var month = 1;
      var dateQ2 = from bo in session.EntitySet<IBookOrder>()
                   where bo.CreatedOn < new DateTime(year, month, 1)
                   select bo;
      var listByDate2 = dateQ2.ToList();
      Assert.IsTrue(listByDate2.Count > 1, "Query with new DateTime(vars) failed. ");

      // Now with new auto object
      var doraOrdersInfo = session.EntitySet<IBookOrder>().Where(o => o.User.UserName == "Dora")
                                   .Select(o => new { Order = o, Total = o.Total }).ToList();
      Assert.IsTrue(doraOrdersInfo.Count > 0, "new auto failed.");

      // more complex
      // Does not work currently, something broken with joins
      var orderLines = session.EntitySet<IBookOrderLine>().Where(ol => ol.Price > 1)
                                .Select(ol => new { Line = ol, Book = ol.Book, Customer = ol.Order.User }).ToList();
      Assert.IsTrue(orderLines.Count > 0, "expected order lines.");

      /*
            //and even more complex  - this does not work currently; JOIN algorithm is broken; and using First is broken
            var reviews = from r in session.EntitySet<IBookReview>()
                              select new { Id = r.Id, FirstAuthor = r.Book.Authors.First().User };
            var reviewList = reviews.ToList();
       */

    }


    private bool IsAttachedTo(object entity, IEntitySession session) {
      var recSession = EntityHelper.GetSession(entity);
      return recSession == session;
    }

    [TestMethod]
    public void TestLinqLikeOperator() {
      //We test escaping wildcards
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var reviews = session.EntitySet<IBookReview>();
      // Using literal, with underscore and apostrophe
      var rList = reviews.Where(r => r.Caption.StartsWith("_")).ToList();
      Assert.AreEqual(1, rList.Count, "Expected only one review");
      Assert.AreEqual("_Boo", rList[0].Review, "Expected '_Boo' in review body.");
      //apostrophe
      rList = reviews.Where(r => r.Caption.StartsWith("'")).ToList();
      Assert.AreEqual(1, rList.Count, "Expected only one review");
      Assert.AreEqual("'Boo", rList[0].Review, "Expected <'Boo> in review body.");

      // Using SQL parameter
      CheckSingleMatch(reviews, "_", "_Boo");
      CheckSingleMatch(reviews, "%", "%Boo");
      CheckSingleMatch(reviews, "[", "[Boo]");
      CheckSingleMatch(reviews, "]", "]Boo[");
      CheckSingleMatch(reviews, @"/", @"/Boo/oo");
      CheckSingleMatch(reviews, @"\", @"\Boo\oo");
    }

    private void CheckSingleMatch(IQueryable<IBookReview> reviews, string pattern, string expectBody) {
      // Pattern from method parameter is automatically translated into SQL parameter
      var q = from r in reviews
              where r.Caption.StartsWith(pattern)
              select r;
      var result = q.ToList();
      Assert.AreEqual(1, result.Count, "Expected only one review. Pattern: " + pattern);
      var msg = string.Format("Expected '{0}' in review body. Pattern: '{1}'", expectBody, pattern);
      Assert.AreEqual(expectBody, result[0].Review, msg);
    }

    [TestMethod]
    public void TestLinqJoins() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var books = session.EntitySet<IBook>();
      var pubs = session.EntitySet<IPublisher>();
      var authors = session.EntitySet<IAuthor>();
      var users = session.EntitySet<IUser>();

      // Simplest INNER join
      var qInner = from b in books
                   select new { B = b, P = b.Publisher };
      var listInner = qInner.ToList();
      Assert.IsTrue(listInner.Count > 0, "Expected some books with publishers");

      // Simplest LEFT OUTER join  on nullable property (IAuthor.User is nullable) 
      // returns all authors that are or are not not users; 
      // note that even for authors with a.User==null, the query does not fail and a.User.UserName evaluates to null
      var qOuter = from a in authors
                   select new { A = a, U = a.User, UserName = a.User.UserName };
      var listOuter = qOuter.ToList();
      Assert.IsTrue(listOuter.Count > 1, "Expected > 1 authors in outer join");

      // Similar query but with INNER join, with join condition in WHERE clause
      // should return authors that are users only
      var qInner2 = from u in users
                    from a in authors
                    where a.User == u
                    select new { A = a, U = u };
      var listInner2 = qInner2.ToList();
      Assert.IsTrue(listInner2.Count > 0, "inner join with where failed.");
      Assert.IsTrue(listInner2.Count < listOuter.Count, "expected less rows in inner join");

      // More advanced scenarios
      // Join of sub queries using multiple 'from' + 'where' (without 'join' keyword); 
      // Note that joins using 'join' keyword do not support subqueries
      // also checking use of parameter inside sub-query
      string nonName = "NoName";
      var subUsers = users.Where(u => u.IsActive && u.UserName != "noname");
      var subAuthors = authors.Where(a => a.FirstName != nonName);
      var qSubQueries = from u in subUsers
                        from a in subAuthors
                        where a.User == u
                        select new { A = a, U = u };
      var listSubQueries = qSubQueries.ToList();
      Assert.IsTrue(listSubQueries.Count > 0, "Sub-queries join failed.");

      // simple join thru property. 
      var qjp = from a in authors
                select new { User = a.User, Author = a };
      var lstQjp = qjp.ToList();
      Assert.IsTrue(lstQjp.Count > 0, "Failed to retrieve users with authors with users.");

      // Cross join
      // RI: join condition on Entities (ent equals ent) is handled ok, automatically changed to comparing keys, just like in WHERE
      var booksWithPubs = from b in books
                          join p in pubs on b.Publisher equals p
                          select new { Book = b, Publisher = p };
      var lstBooksWithPubs = booksWithPubs.ToList();
      Assert.IsTrue(lstBooksWithPubs.Count > 0, "Books-Join-Publishers test returned 0 results.");

      // Outer join in a fancy way, MSDN-style. See here: http://msdn.microsoft.com/en-us/library/vstudio/bb397895.aspx
      var qUsersWithAuthors = from u in users
                              join a in authors on u.Id equals a.User.Id into tempAuthors
                              from a2 in tempAuthors.DefaultIfEmpty()
                              select new { UserName = u.UserName, AuthorLastName = a2.LastName };
      //last name is null if user is not author (a2 is null) - works OK
      var lstUsersWithAuthors = qUsersWithAuthors.ToList();
      Assert.IsTrue(lstUsersWithAuthors.Count > 0, "Failed to retrieve users with authors.");

    }

    [TestMethod]
    public void TestLinqLimitOffset() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var bookOrders = session.EntitySet<IBookOrder>();
      // Skip and Take arguments are always included into expr tree as constants - see Queryable.Skip,Take definitions
      // (they take 'int' as argument, not expression). 
      // The result is that the same query for different page 'looks' differently. VITA automatically converts 
      // these constants into parameters so it can reuse the query. In this test we check that this actually happens. 
      //One argument to Skip/take is local var, another is constant - both are translated into parameters.
      int skip = 1;
      var someOrders = bookOrders.Skip(skip).Take(2).ToList();
      Assert.IsTrue(someOrders.Count > 0, "Paged query failed.");
      //Check parameters count
      var cmd = session.GetLastCommand();
      Assert.AreEqual(2, cmd.Parameters.Count, "Invalid param count for page query.");
      // We used Skip/Take; this requires specifying OrderBy in MsSql, Postgres. 
      // Linq engine should be adding fake order-by clause: ORDER BY (SELECT 1)
      if(Startup.ServerType == DbServerType.MsSql || Startup.ServerType == DbServerType.Postgres) {
        var sql = cmd.CommandText.ToUpperInvariant();
        Assert.IsTrue(sql.IndexOf("ORDER BY (SELECT 1)") > 0, "Expected fake OrderBy clause");
      }
      //try with only skip and only take
      var allOrdCount = bookOrders.Count();
      someOrders = bookOrders.Skip(1).ToList();
      Assert.AreEqual(allOrdCount - 1, someOrders.Count, "Expected some orders");
      someOrders = bookOrders.Take(2).ToList();
      Assert.AreEqual(2, someOrders.Count, "Expected 2 orders");
    }

    [TestMethod]
    public void TestLinqArrayContains() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();

      var bookOrders = session.EntitySet<IBookOrder>();
      //Note: for debugging use table that is not fully cached, so we use IBookOrder entity

      // Test retrieving orders by Id-in-list
      var someOrders = bookOrders.Take(2).ToList();
      var someOrderIds = someOrders.Select(o => o.Id).ToArray();
      var qSomeOrders = from bo in bookOrders
                        where someOrderIds.Contains(bo.Id)
                        select bo;
      var someOrders2 = qSomeOrders.ToList();
      var cmd = session.GetLastCommand(); //just for debugging
      Assert.AreEqual(someOrderIds.Length, someOrders2.Count, "Test Array.Contains failed: order counts do not match.");

      // Try again with a single Id
      var arrOneId = new Guid[] { someOrderIds[0] };
      var qOrders = from bo in bookOrders
                    where arrOneId.Contains(bo.Id)
                    select bo;
      var orders = qOrders.ToList();
      Assert.AreEqual(1, orders.Count, "Test Array.Contains with one Id failed: order counts do not match.");

      // Again with empty list
      var arrEmpty = new Guid[] { };
      var qNoBooks = from b in session.EntitySet<IBook>()
                     where arrEmpty.Contains(b.Id)
                     select b;
      var noBooks = qNoBooks.ToList();
      cmd = session.GetLastCommand();
      Assert.AreEqual(0, noBooks.Count, "Test Array.Contains with empty array failed, expected 0 entities");

      // Empty list, no parameters option - should be 'literal empty list' there, depends on server type
      qNoBooks = from b in session.EntitySet<IBook>().WithOptions(QueryOptions.NoParameters)
                 where arrEmpty.Contains(b.Id)
                 select b;
      noBooks = qNoBooks.ToList();
      cmd = session.GetLastCommand();
      Assert.AreEqual(0, noBooks.Count, "Expected 0 entities, empty-list-contains with literal empty list");
      Assert.AreEqual(0, cmd.Parameters.Count, "Expected 0 db params with NoParameters option");

      // Again with list, not array
      var orderIdsList = someOrderIds.ToList();
      qOrders = from bo in bookOrders
                where orderIdsList.Contains(bo.Id)
                select bo;
      orders = qOrders.ToList();
      Assert.AreEqual(orderIdsList.Count, orders.Count,
          "Test constList.Contains, repeated query failed: order counts do not match.");

      // Again with NoParameters options - force using literals
      qOrders = from bo in bookOrders.WithOptions(QueryOptions.NoParameters)
                where orderIdsList.Contains(bo.Id)
                select bo;
      orders = qOrders.ToList();
      Assert.AreEqual(orderIdsList.Count, orders.Count,
          "Test constList.Contains, no-parameters linq query failed: order counts do not match.");
      cmd = session.GetLastCommand();
      Assert.AreEqual(0, cmd.Parameters.Count, "NoParameters option - expected no db parameters");


      // Test intList.Contains()
      var userTypes = new UserType[] { UserType.Customer, UserType.Author };
      var qOrders2 = from bo in bookOrders
                     where userTypes.Contains(bo.User.Type)
                     select bo;
      var orders2 = qOrders2.ToList();
      Assert.IsTrue(orders2.Count > 0, "No orders by type found.");
    }

    [TestMethod]
    public void TestSqlCache() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var books = session.EntitySet<IBook>();
      var authors = session.EntitySet<IAuthor>();

      //Testing how expr like 'where someEnt.OtherEnt == null' are handled
      var qa = from a in authors
               where a.User == null
               select a;
      var aList = qa.ToList(); // just checking SQL is correct and it runs


      //test compiled query caching
      var kidBooksQ = from b in books
                      where b.Publisher.Name == "Kids Books"
                      orderby b.Title
                      select b;

      var lkpCount0 = SqlCache.LookupCount;
      var missCount = SqlCache.MissCount; 
      var kidBooksList = kidBooksQ.ToList();
      Assert.IsTrue(kidBooksList.Count > 0, "kid books not found.");
      Assert.AreEqual(lkpCount0 + 1, SqlCache.LookupCount, "Expected one failed sql cache lookup");
      Assert.AreEqual(missCount + 1, SqlCache.MissCount, "Expected one failed sql cache lookup");
      //do it  2 more times
      kidBooksList = kidBooksQ.ToList();
      kidBooksList = kidBooksQ.ToList();
      Assert.IsTrue(kidBooksList.Count > 0, "kid books not found.");
      Assert.AreEqual(lkpCount0 + 3, SqlCache.LookupCount, "Expected 2 more lookup");
      Assert.AreEqual(missCount + 1, SqlCache.MissCount, "Expected no more misses");

      // Query with disabled compiled query cache. Disable cache for 'search' queries, to avoid polluting cache
      // with many custom searches that users enter in search form. 
      // Note about the test query - we must make sure it is of unique shape, otherwise system will pickup previously compiled query.
      var noCacheBooksQ = session.EntitySet<IBook>().WithOptions(QueryOptions.NoQueryCache);
      var kidBooksQDesc = from b in noCacheBooksQ
                          where b.Publisher.Name == "Kids Books"
                          orderby b.Title descending
                          select b;
      var kidBooksListDesc = kidBooksQ.ToList();
      Assert.IsTrue(kidBooksListDesc.Count > 0, "Query with disabled query cache failed: multiple-author books not found.");
    }

    [TestMethod]
    public void TestLinqBoolBitColumns() {
      // We test that LINQ engine correctly handles bit fields
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var users = session.EntitySet<IUser>();

      bool boolParam = true;
      //Bug fix, handling expressions like 'ent.BoolProp & boolValue == ent.BoolProp'
      var q0 = from u in users
               where (u.IsActive && boolParam) == u.IsActive
               select u;
      var lstUsers0 = q0.ToList();

      // MS SQL, MySql : bit field is integer, so
      // expression over bit field: 'u.IsActive==true' should be replaced with 'u.IsActive = 1'; we also check ! operator and bool parameter
      // Postgress has boolean data type, so it should be used as is.
      var q2 = from u in users
               where u.IsActive && u.IsActive == true && u.IsActive == boolParam && true || boolParam || !u.IsActive
               select u;
      var lstUsers2 = q2.ToList();
      LogLastQuery(session);
      Assert.IsTrue(lstUsers2.Count > 0, "Bit field expr test failed");

      // bool/bit field used in Where expressions directly - should be replaced with 'u.IsActive = 1'
      var q1 = from u in users
               where u.IsActive
               select u;
      var lstUsers1 = q1.ToList();
      Assert.IsTrue(lstUsers1.Count > 0, "No active users found.");

      // Using bit field in anon type initializer
      var q3 = from u in users
                 //where u.IsActive
               select new { U = u.UserName, A = u.IsActive };
      var lstUsers3 = q3.ToList();
      Assert.IsTrue(lstUsers3.Count > 0, "Bit field - use in anon object failed.");

      Startup.BooksApp.Flush();
    }


    [TestMethod]
    public void TestLinqBoolOutputColumns() {
      // We test that LINQ engine correctly handles bool values in return columns
      var app = Startup.BooksApp;
      var session = app.OpenSession();

      // Some servers (MS SQL, Oracle) do not support bool as valid output type
      //   So SQL like "SELECT (2 > 1) As BoolValue" fails
      // LINQ engine automatically adds IIF(<boolValue>, 1, 0)
      // Another trouble: MySql stores bools as UInt64, but comparison results in Int64
      // Oracle does not allow queries without FROM, so engine automatically adds 'FROM dual' which if fake FROM clause
      session = app.OpenSession();
      var hasFiction = session.EntitySet<IBook>().Any(b => b.Category == BookCategory.Fiction);
      Assert.IsTrue(hasFiction, "Expected hasFiction to be true");

      // another variation
      var books = session.EntitySet<IBook>().Where(b => b.Authors.All(a => a.LastName != null)).ToArray();
      Assert.IsTrue(books.Length > 0, "Expected all books");


    }
    [TestMethod]
    public void TestLinqContains() {

      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var orderLines = session.EntitySet<IBookOrderLine>();

      // Find books that have NOT been ordered - try by IDs
      var q0 = from b in session.EntitySet<IBook>()
               where !orderLines.Select(bol => bol.Book.Id).Contains(b.Id)
               select b;
      var list0 = q0.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list0.Count > 0, "Query for not ordered books (Ids) failed.");

      // The same, only using directly book reference
      // entitySet.Contains(ent) - this also works
      var qBooksNotPurchased = from b in session.EntitySet<IBook>()
                               where !orderLines.Select(bol => bol.Book).Contains(b)
                               select b;
      var lstBooksNotPurchased = qBooksNotPurchased.ToList();
      LogLastQuery(session);
      Assert.IsTrue(lstBooksNotPurchased.Count > 0, "Query with Contains(book) failed.");

      //One very special case - pub.Books.Contains(...) method. In this case, Contains is not Queryable.Contains, but ICollection.Contains(item)
      // - because pub.Books is IList<IBook>, so compiler picks Contains instance method on the class/interface, before extension method. 
      // SQL translator makes special treatment of this case
      var csBook = session.EntitySet<IBook>().First(b => b.Title == "c# Programming");
      var qCsBkPub = from pub in session.EntitySet<IPublisher>()
                     where pub.Books.Contains(csBook)
                     select pub;
      var csBookPubs = qCsBkPub.ToList();
      LogLastQuery(session);
      Assert.IsTrue(csBookPubs.Count == 1, "Query publisher by book failed. ");

    } //method

    [TestMethod]
    public void TestLinqSpecialQueries() {
      //Some seemingly simple queries that caused problems for LINQ engine initially
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var books = session.EntitySet<IBook>();
      var pubs = session.EntitySet<IPublisher>();
      IDbCommand cmd;

      // Join/Distinct/Count combinations. Encountered error: The column 'Id' was specified multiple times for 'X'. 
      // does not work for Oracle - the produced SQL has GroupBy with all output columns; book.Abstract is blob text, a
      // and Oracle does not allow such columns in GroupBy
      if(Startup.ServerType != DbServerType.Oracle) {
        var qJoin = from b in books.WithOptions(QueryOptions.ForceIgnoreCase)
                    join p in pubs on b.Publisher equals p
                    where p.Name == "MS Books"
                    select new { B = b, P = p }; //
        var joinDistinct = qJoin.Distinct().ToList();
        Assert.IsTrue(joinDistinct.Count > 0, "JoinDistinct query failed.");
        var joinCount = qJoin.Count();
        Assert.IsTrue(joinCount > 0, "JoinCount query failed.");
        var joinDistinctCount = qJoin.Distinct().Count();
        Assert.IsTrue(joinDistinctCount > 0, "JoinDistinctCount query failed.");
        var joinDistinctTake = qJoin.Distinct().Take(3).ToList();
        Assert.IsTrue(joinDistinctTake.Count > 0, "JoinDistinctTake query failed.");
        //var joinDistinctSkipTake = qJoin.Distinct().Skip(1).Take(3).ToList();
        var joinDistinctSkipTake = qJoin.Distinct().Skip(1).Take(2).ToList();
        cmd = session.GetLastCommand();
        Assert.IsTrue(joinDistinctSkipTake.Count > 0, "JoinDistinctSkipTake query failed.");
      } // if !Oracle

      // New operator hidden inside Count(). Added special handling of New in SqlBuilder
      var qJoin2 = from b in books
                   select new { Title = b.Title };
      var joinDistinctCount2 = qJoin2.Count();
      Assert.IsTrue(joinDistinctCount2 > 0, "Count query failed.");

      //Take/skip
      var qbase = books.Where(b => b.Publisher.Name == "MS Books");
      // with order by
      var qTakeSkip = qbase.OrderBy(b => b.Title).Skip(1).Take(2);
      var lTakeSkip = qTakeSkip.ToList();
      cmd = session.GetLastCommand();
      Assert.AreEqual(2, lTakeSkip.Count, "TakeSkip query failed.");
      // without Order by - should automatically add 'order by (Select 1)' (Take requires OrderBy by some servers)
      qTakeSkip = qbase.Skip(1).Take(2);
      lTakeSkip = qTakeSkip.ToList();
      Assert.AreEqual(2, lTakeSkip.Count, "TakeSkip query failed.");

      //using min/max dates - parameter should use DbType.DateTime2, not DateTime to avoid overflow
      var qByDates = books.Where(b => b.PublishedOn > DateTime.MinValue && b.PublishedOn < DateTime.MaxValue);
      var lByDates = qByDates.ToList();
      Assert.IsTrue(lByDates.Count > 0, "MinDate query failed");

      // Some servers: when using OFFSET and LIMIT clauses, ORDER BY must be specified
      // If not, LINQ engine must add default ( 'ORDER BY (SELECT 1)' or by primary key)
      var qSkipTake = books.Skip(1).Take(1);
      var lSkipTake = qSkipTake.ToList();
      Assert.AreEqual(1, lSkipTake.Count, "Skip/take without order by query failed");

      // Simplest query - not so simple, had to fix some code to make it work
      var allBooks = books.ToList();
      Assert.IsTrue(allBooks.Count > 0, "books.ToList() failed.");

      // Aggregate functions
      // Simple query for max
      var maxPrice = session.EntitySet<IBook>().Max(bk => bk.Price);
      Assert.IsTrue(maxPrice > 0, "Max price query failed.");

      // Embedding subquery for max price inside another query. 
      var qBookWMaxPrice = from b in session.EntitySet<IBook>()
                           where b.Price == session.EntitySet<IBook>().Max(b1 => b1.Price)
                           select b;
      var lmax = qBookWMaxPrice.ToList();
      Assert.IsTrue(lmax.Count > 0, "Expected non-empty list.");

      var qBookWMaxPriceInCat = from b in session.EntitySet<IBook>()
                                where b.Price == session.EntitySet<IBook>()
                                                  .Where(b1 => b1.Category == b.Category).Max(b1 => b1.Price)
                                select b;
      var lmax2 = qBookWMaxPriceInCat.ToList();
      Assert.IsTrue(lmax2.Count > 0, "Expected non-empty list.");

      // Test ForceIgnoreCase option
      // does not work for Oracle, no way to force case-insensitive on query level - 
      // only by explicitly using Title.ToUpper() in linq query
      if(Startup.ServerType != DbServerType.Oracle) {
        session = app.OpenSession();
        var q = session.EntitySet<IBook>().Where(b => b.Title == "vb pRogramming" && b.Title.StartsWith("vb"))
          .WithOptions(QueryOptions.ForceIgnoreCase);
        var vbBk = q.FirstOrDefault();
        Assert.IsTrue(vbBk != null, "Case-insensitive match failed");
      } // if !Oracle
    }

    [TestMethod]
    public void TestLinqWithNullables() {
      var session = Startup.BooksApp.OpenSession();
      session.EnableCache(false); //We are testing real SQLs

      //First query using literal null; this alwasy worked OK, SQL generated is "WHERE b.Abstract IS NULL"
      var qNotPublished = from b in session.EntitySet<IBook>()
                                  where b.PublishedOn == null
                                  select b;
      var booksNotPublished = qNotPublished.ToList();
      var countNotPublished = booksNotPublished.Count;
      Assert.IsTrue(countNotPublished > 0, "Expected non-published book ");

      // Bug fix. Using a variable instead of literal null.
      //   Before fix: the query was using 'WHERE b.PublishedOn = @P1", which fails to match null values
      //   After fix: the SQL is 'WHERE (b.Abstract == @P1 OR (b.Abstract IS NULL) AND (@P1 IS NULL))'
      DateTime? nullDate = null;
      var qNotPublished2 = from b in session.EntitySet<IBook>()
                                  where b.PublishedOn == nullDate
                                  select b;
      var booksNotPublished2 = qNotPublished2.ToList();
      var countNotPublished2 = booksNotPublished2.Count;
      Assert.AreEqual(countNotPublished, countNotPublished2, "Null query failed, expected the same book count.");


      // Nullable entity refs and strings
      IUser someEditor = null;
      string someCode = null;
      var qNullCompare = from b in session.EntitySet<IBook>()
                         where b.SpecialCode == someCode || b.Editor == someEditor  
                         select b;
      var booksNullCompare = qNullCompare.ToList();
      var cmd = session.GetLastCommand();
      Assert.IsTrue(booksNullCompare.Count > 0, "Expected some books without editor");



    }

    // [TestMethod]
    public void TestLinqUnionExcept() {
      //Some seemingly simple queries that caused problems for LINQ engine initially
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var books = session.EntitySet<IBook>();
      var pubs = session.EntitySet<IPublisher>();

      //simple union
      var qUnion = books.Where(b => b.Title == "c# Programming").Union(books.Where(b => b.Title == "VB Programming"));
      var lstUnion = qUnion.ToList();
      Assert.IsTrue(lstUnion.Count == 2, "Union query failed.");

      // Union of anon types
      var q1 = books.Where(b => b.Title == "c# Programming").Select(b => new { Id = b.Id, Title = b.Title });
      var q2 = books.Where(b => b.Title == "VB Programming").Select(b => new { Id = b.Id, Title = b.Title });
      var q12 = q1.Union(q2);
      var list12 = q12.ToList();
      Assert.IsTrue(list12.Count == 2, "Union query2 failed.");

      // Except
      // Only MsSql and Postgres support Except
      bool supportsExcept = Startup.ServerType == DbServerType.MsSql || Startup.ServerType == DbServerType.Postgres;
      // for cached entities, the comparisons in filters are performed for CLR AUTO objects (.NET-produced comparison), and they do not work
      // correctly. Smth we have to live with - no way of fixing auto objects comparison
      if (supportsExcept) {
        var qe1 = books.Where(b => b.Title.Contains("Programming")).Select(b => new { Id = b.Id, Title = b.Title });
        var qe2 = books.Where(b => b.Title.Contains("c#")).Select(b => new { Id = b.Id, Title = b.Title });
        var qe12 = qe1.Except(qe2);
        var listEx12 = qe12.ToList(); // we should have VB progr, Windows progr, but not c# progr
        LogLastQuery(session);
        Assert.IsTrue(listEx12.Count == 2, "Except query2 failed.");
      }
    }

    //There are 4 overloads of Queryable.GroupBy that we need to support (another 4 with IComparer as last parameter are not supported).
    // Test them all here.
    [TestMethod]
    public void TestLinqGroupBy() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      session.EnableCache(false); 

      //overload #1; for this method grouping occurs in CLR
      var query1 = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id);
      var list1 = query1.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list1.Count > 0, "overload #1 failed.");

      //overload #2; grouping in CLR
      // Select pairs of publisher id, book titles
      var query2 = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id, b => b.Title); ;
      var list2 = query2.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list2.Count > 0, "overload #2 failed.");
      //slight variation of #2, with autotype and New
      var query2b = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id, b => new { Title = b.Title, Price = b.Price }); ;
      var list2b = query2b.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list2b.Count > 0, "overload #2-B failed.");
      
      //overload #3 - grouping in SQL
      // Select pairs of publisher id, book count, average price
      var query3 = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id, 
        (id, books) => new {Id = id, Count = books.Count(), AvgPrice = books.Average(b=>b.Price)});
      var list3 = query3.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list3.Count > 0, "overload #3 failed.");


      // SQL-like syntax Group-By
      var booksByCat = from b in session.EntitySet<IBook>()
                       group b by b.Category into g
                       orderby g.Key
                       select new { Category = g.Key, BookCount = g.Count(), MaxPrice = g.Max(b => b.Price) };
      var lstBooksByCat = booksByCat.ToList();
      LogLastQuery(session);
      Assert.IsTrue(lstBooksByCat.Count > 0, "GroupBy test returned 0 groups.");

      //Some special queries
      // - groupBy followed by Select
      var queryS1 = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id).Select(g => new { Id = g.Key, Count = g.Count() });
      var listS1 = queryS1.ToList();
      LogLastQuery(session);
      Assert.IsTrue(listS1.Count > 0, "Special test 1 failed.");

      // -- select followed by GroupBy
      var queryS2 = session.EntitySet<IBook>().Join(
          session.EntitySet<IPublisher>(), b => b.Publisher.Id, p => p.Id, (b, p) => new { PubId = p.Id, BookId = b.Id })
               .GroupBy(bp => bp.PubId, (pubId, pairs) => new { PubId = pubId, BookCount = pairs.Count() });
      var listS2 = queryS2.ToList();
      LogLastQuery(session);
      Assert.IsTrue(listS2.Count > 0, "Special test 2 (select followed by GroupBy) failed.");

      // GroupBy nullable field - this is a special case; Null values will come out as type default in group key;
      // So for authors that are not users (author.User is null), the resulting group will have key = Guid.Empty
      // this fails with entity cache
      session.EnableCache(false);
      var queryS3 = session.EntitySet<IAuthor>().GroupBy(a => a.User.Id).Select(g => new { Id = g.Key, Count = g.Count() });
      var listS3 = queryS3.ToList();
      LogLastQuery(session);
      Assert.IsTrue(listS3.Count > 0, "Special test 3 (Group by nullable key) failed.");

      //Aggregates with fake group by - returning agregates without group by
      var dora = session.EntitySet<IUser>().First(u => u.UserName == "Dora");
      //get count, average of book order
      var doraOrderStats = from ord in session.EntitySet<IBookOrder>()
                       where ord.User == dora
                       group ord by 0 into g
                       select new { Count = g.Count(), Avg = g.Average(o => o.Total) };
      var stats = doraOrderStats.ToList();
      Assert.AreEqual(1, stats.Count, "Expected 1 stats record");
      var stat0 = stats[0]; 
      Assert.IsTrue(stat0.Count > 0 && stat0.Avg > 0, "Expected non-zero count and avg.");
      /* SQL: 
          SELECT COUNT_BIG(*) AS "Count", AVG("Total") AS "Avg"
          FROM "books"."BookOrder"
          WHERE ("User_Id" = @P0)
       */

    }

    [TestMethod]
    public void TestLinqEntityListMembers() {
      
      var app = Startup.BooksApp;
      var session = app.OpenSession();

      // One-to-many relationship. Use publisher.Books member in LINQ query - this should result in sub-query against Book table
      // Find publisher with at least one book priced above $10
      var qPubsWExpBooks = from p in session.EntitySet<IPublisher>()
                            where p.Books.Any(b => b.Price > 10)
                            select p;
      var lstPubsWExpBooks = qPubsWExpBooks.ToList();
      LogLastQuery(session);
      // print out query and command
      Assert.IsTrue(lstPubsWExpBooks.Count > 0, "Query for publisher of books with price > 10 failed."); // should find 'MS Publishing'

      // Many-to-many relationship. Use book.Authors in LINQ query query - this should result in sub-query against join of BookAuthor->Author table
      // Find books written by author with last name 'Sharp'
      var qBooksBySharp = from b in session.EntitySet<IBook>()
                          where b.Authors.Any(a => a.LastName == "Sharp")
                          select b;
      var lstBooksBySharp = qBooksBySharp.ToList();
      LogLastQuery(session);
      Assert.AreEqual(2, lstBooksBySharp.Count, "Query for books by author 'Sharp' failed."); // 'c# progr' and 'win prog' books

      //Aggregate functions against Lists in properties (child entities)
      var qBookCountByPub = session.EntitySet<IPublisher>().Select(p => new { PubName = p.Name, Id = p.Id, BookCount = p.Books.Count() });
      var listBookCountByPub = qBookCountByPub.ToList();
      LogLastQuery(session);
      Assert.IsTrue(listBookCountByPub.Count > 0, "Book count by pub query failed.");

    }


    [TestMethod]
    public void TestLinqQueryFilter() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      session.EnableCache(false); 
      var filter = session.Context.QueryFilter;

      // Add automatic filter for programming-only books
      filter.Add<IBook>(b => b.Category == BookCategory.Programming);
      var progBooks = session.EntitySet<IBook>().ToList();
      Assert.IsTrue(progBooks.Count > 0, "Expected at least 1 progr book.");
      var allAreProgr = progBooks.All(b => b.Category == BookCategory.Programming);
      Assert.IsTrue(allAreProgr, "Expected only programming books");

      // with reading value from operation context
      var context = session.Context;
      context.SetValue("BookCat", BookCategory.Programming);
      filter.Clear();
      filter.Add<IBook, OperationContext>((b, ctx) => b.Category == ctx.GetValue<BookCategory>("BookCat"));
      progBooks = session.EntitySet<IBook>().ToList();
      Assert.IsTrue(progBooks.Count > 0, "Expected at least 1 progr book.");
      allAreProgr = progBooks.All(b => b.Category == BookCategory.Programming);
      Assert.IsTrue(allAreProgr, "Expected only programming books");

      // Simpler version, with injecting parameter; bookCat will be retrieved from context.Values (by name 'bookCat')
      filter.Clear();
      filter.Add<IBook, BookCategory>((b, bookCat) => b.Category == bookCat);
      progBooks = session.EntitySet<IBook>().ToList();
      allAreProgr = progBooks.All(b => b.Category == BookCategory.Programming);
      Assert.IsTrue(progBooks.Count > 0, "Expected at least 1 progr book.");
      Assert.IsTrue(allAreProgr, "Expected only programming books");
      // With subquery on authors (authors count)
      filter.Clear();
      filter.Add<IBook>(b => b.Authors.Count > 1);
      var maBooks = session.EntitySet<IBook>().ToList();
      Assert.IsTrue(maBooks.Count > 0, "Expected multi-author books");
      var allAreMultiAuthor = maBooks.All(b => b.Authors.Count > 1);
      Assert.IsTrue(allAreMultiAuthor, "Expected only multi-author books.");

      // User Id injection
      var loginService = app.GetService<ILoginService>();
      var doraContext = app.CreateSystemContext();
      var password = Samples.BookStore.SampleData.SampleDataGenerator.DefaultPassword;
      var doraLogin = loginService.Login(doraContext, "dora", password);
      Assert.AreEqual(LoginAttemptStatus.Success, doraLogin.Status, "Login failed.");
      var doraSession = doraContext.OpenSession();
      doraSession.EnableCache(false);
      //Add filter to restrict orders to only current user's orders
      doraContext.QueryFilter.Add<IBookOrder, Guid>((bo, userid) => bo.User.Id == userid);
      // Now get 'all' orders - it should return only Dora's orders
      var doraOrders = doraSession.EntitySet<IBookOrder>().ToList();
      Assert.IsTrue(doraOrders.Count > 0, "Failed to find Dora's orders.");


      
    }

    private static void LogLastQuery(IEntitySession session) {
/*
      Debug.WriteLine("---------------------------------------------------------------------------------------");
      var lastCmd = session.GetLastLinqCommand();
      if(lastCmd != null)
        Debug.WriteLine("Query: " + lastCmd.ToString());
      var command = session.GetLastCommand();
      if(command != null)
        Debug.WriteLine("SQL:" + command.CommandText);
 */ 
    }


    [TestMethod]
    public void TestLinqReturnCustomObject() {
      var session = Startup.BooksApp.OpenSession();
      var books = session.EntitySet<IBook>();

      // query with custom type in output (not anon type)
      var qBkInfos = from b in books
                     where b.Price > 1
                     select new BookInfo() {Price = b.Price,  Title = b.Title, Publisher = b.Publisher.Name};
      var lstBkInfos = qBkInfos.ToList();
      Assert.IsTrue(lstBkInfos.Count > 0, "BookInfo query failed.");

      // Same with non-default constructor
      var qBkInfos2 = from b in books
                      where b.Price > 1
                      select new BookInfo(b.Title, b.Publisher.Name, b.Price) { Title = b.Title };
      var lstBkInfos2 = qBkInfos2.ToList();
      Assert.IsTrue(lstBkInfos2.Count > 0, "BookInfo query failed.");

      // bug fix - Linq with out object filled from GroupBy over nullable key
      // book.Editor is nullable; b.Editor.Id is translated into Guid? expression. 
      // Linq engine adds a conversion that return default(Guid) if coming value is null. 
      // We also test enum and string values
      var bkCounts = books
        .Select(b => new EditorObj() {
          EditorId = b.Editor.Id, UserName = b.Editor.UserName, UserType = b.Editor.Type
        }
        ).ToList();
      Assert.IsTrue(bkCounts.Count > 0, "Expected some objects");

    }

    //Helper class to use as output in queries - testing LINQ engine with custom (non-anon) output types
    [DebuggerDisplay("{Title},{Publisher},{Price}")]
    class BookInfo {
      public string Title;
      public string Publisher;
      public decimal Price;

      public BookInfo() { }
      public BookInfo(string title, string publisher, decimal price) {
        Title = title;
        Publisher = publisher;
        Price = price;
      }
    }

    class EditorObj {
      public Guid EditorId;
      public UserType UserType;
      public string UserName; 
    }


    [TestMethod]
    public void TestLinqDates() {
      // SQLite date/time functions return strings, so tests do not work
      if (Startup.ServerType == DbServerType.SQLite) 
        return; 

      var session = Startup.BooksApp.OpenSession();
      var books = session.EntitySet<IBook>();
      var bk1 = books.First();

      session.EnableCache(false);
      var createdOn = bk1.CreatedOn;
      IList<IBook> bookList;
      IDbCommand cmd; 

      bookList = books.Where(b => b.CreatedOn.Date == createdOn.Date).ToList();
      cmd = session.GetLastCommand();
      Assert.IsTrue(bookList.Count > 0, "Expected some book by CreatedOn.Date");

      switch (Startup.ServerType) {
        //MySql, Postgres, Oracle TIME() not supported
        case DbServerType.MySql: case DbServerType.Postgres:   case DbServerType.Oracle:
          break; 
        default: 
          bookList = books.Where(b => b.CreatedOn.TimeOfDay == createdOn.TimeOfDay).ToList();
          cmd = session.GetLastCommand();
          Assert.IsTrue(bookList.Count > 0, "Expected some book by CreatedOn.TimeOfDay");
          break; 
      }

      bookList = books.Where(b => b.CreatedOn.Year == createdOn.Year).ToList();
      cmd = session.GetLastCommand();
      Assert.IsTrue(bookList.Count > 0, "Expected some book by CreatedOn.Year");

      bookList = books.Where(b => b.CreatedOn.Day == createdOn.Day).ToList();
      cmd = session.GetLastCommand();
      Assert.IsTrue(bookList.Count > 0, "Expected some book by CreatedOn.Day");
    }

    [TestMethod]
    public void TestLinqArrayParameters() {
      // We test MsSql and Postgres here, but it should work for other servers - for these arrays will be converted to literals
      var session = Startup.BooksApp.OpenSession();
      session.EnableCache(false);
      // count prog books reviews 
      var progReviewCount = session.EntitySet<IBookReview>().Where(r => r.Book.Category == BookCategory.Programming).Count(); 
      var progBooks = session.EntitySet<IBook>().Where(b => b.Category == BookCategory.Programming).ToList();

      //This array will be passed to db as parameter; MS SQL - converted to DataTable, Postgres - as an array
      var bookIds = progBooks.Select(b => b.Id).ToArray();
      var reviewQuery = session.EntitySet<IBookReview>().Where(r => bookIds.Contains(r.Book.Id));
      var reviews = reviewQuery.ToList();
      Assert.AreEqual(progReviewCount, reviews.Count, "Invalid review count");
      var cmd = session.GetLastCommand();
      //Debug.WriteLine(cmd.CommandText);

      //try list of strings (not array)
      var names = new List<string>(new string[] { "John", "Duffy", "Dora" });
      var selUsers = session.EntitySet<IUser>().Where(u => names.Contains(u.UserName)).ToList();
      Assert.AreEqual(names.Count, selUsers.Count, "Expected some users");
      cmd = session.GetLastCommand();
      //Debug.WriteLine(cmd.CommandText);
    }


    /*
    //[TestMethod]
    public void TestLinqWithEntityCache() {
      if(!Startup.CacheEnabled)
        return;
      Startup.InvalidateCache(waitForReload: true); //make sure cache is reloaded and fresh
      // Some queries (ex: involving many-to-many relations) are not supported by Linq2Sql, 
      // but run OK against full entity cache. 
      // Entity cache is returning clones of entities in cache, and these clones must be attached to the caller session.
      // We test that results of queries are attaced to current session.
      var app = Startup.BooksApp;

      // Many-to-many relations is NOT supported currently, only queries in cache can handle this. It is on TODO list to support m2m. 
      // We check CloneEntities interceptor injected at the top level, for the final results of the query
      var session = app.OpenSession();
      var qMultiAuthorBooks = from b in session.EntitySet<IBook>()
                              where b.Authors.Count > 1
                              orderby b.Title descending
                              select b;
      var multiAuthorBooks = qMultiAuthorBooks.ToList();
      Assert.IsTrue(multiAuthorBooks.Count > 0, "No multi-author books found.");
      Assert.IsTrue(IsAttachedTo(multiAuthorBooks[0], session), "Entities are not attached to current session!");

      // Query returning anon object with entity (Publisher) inside
      // CloneEntity call is injected into an argument of anon object constructor
      session = app.OpenSession();
      var bkInfos = from b in session.EntitySet<IBook>()
                    select new { Title = b.Title, Publisher = b.Publisher };
      var lstBkInfos = bkInfos.ToList();
      Assert.IsTrue(lstBkInfos.Count > 0, "Bk info query failed.");
      Assert.IsTrue(IsAttachedTo(lstBkInfos[0].Publisher, session), "BookInfo.Publisher entity is not attached to current session.");

      // Query returning list of lists in many-to-many; not supported in DB queries, only in entity cache
      // CloneEntities call is injected into lambda parameter of 'Select' method.
      session = app.OpenSession();
      var authQuery = from b in session.EntitySet<IBook>()
                      where b.Category == BookCategory.Programming
                      select b.Authors;
      var authListOfLists = authQuery.ToList();
      Assert.IsTrue(authListOfLists.Count > 0, "Failed to retrieve list-of-lists.");
      Assert.IsTrue(IsAttachedTo(authListOfLists[0][0], session), "Entities in list-of-lists queries are not attached to current session!");
      var authListOfLists2 = authQuery.ToList(); //to check cached query definition

      // checking perf, do it a few more times and look at the log; 
      // The first run of LINQ query in cache - the compilation takes most of the time; the query is compiled and saved in cache,
      // so it should be much faster next run(s)
      var temp1 = qMultiAuthorBooks.ToList();
      temp1 = qMultiAuthorBooks.ToList();
      temp1 = qMultiAuthorBooks.ToList();
      var temp2 = bkInfos.ToList();
      temp2 = bkInfos.ToList();
      temp2 = bkInfos.ToList();
      // yes, it is faster - all queries run with 0ms reported.
    }
    */

  }//class

}
