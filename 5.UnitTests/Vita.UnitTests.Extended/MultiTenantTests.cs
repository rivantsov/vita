using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Entities.Runtime; 
using Vita.Samples.BookStore;
using Vita.Modules.Login;
using Vita.Common;
using System.Globalization;
using Vita.Modules.Login.Api;

using Vita.Data.Driver;
using Vita.Data;

namespace Vita.UnitTests.Extended {

  /* To run multitenant test: 
     1. Create VitaBooks2 empty database in MS SQL Server
     2. Add TEST_MULTI_TENANT compilation condition in this project's properties
     3. Run the test in test explorer
   Multi-tenant test connects the BookStore app to the second database, under data source name 'Books2'. Now you can use Vita sessions to connect to both database, 
   but you need to provide DataSourceName in the operation context (session.Context). If you do not set it explicitly, it is under 'Default' name, and will be connected
   to original VitaBooks database. The test creates 2 objects in VitaBooks2 database (a publisher and a book), verifies that they are there; then if verifies that 
   they are not in the original VitaBooks database.     
   */ 
#if TEST_MULTI_TENANT
  [TestClass]
#endif
  public class MultiTenantTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }

    [TestCleanup]
    public void TearDown() {
      Startup.TearDown(); 
    }

    [TestMethod]
    public void TestMultiTenant() {
      // Multi-tenant test is available only for MS SQL Server.
      if (Startup.ServerType != DbServerType.MsSql)
        return;
      const string Books2 = "VitaBooks2";
      var app = Startup.BooksApp; 
      var connString2 = Startup.ConnectionString.Replace("VitaBooks", Books2);
      var mainDbStt = Startup.DbSettings; 
      var dbSettings2 = new DbSettings(mainDbStt.ModelConfig,  connString2, upgradeMode: DbUpgradeMode.Always, dataSourceName: Books2);
      Vita.UnitTests.Common.TestUtil.DropSchemaObjects(dbSettings2);
      app.ConnectTo(dbSettings2);

      //read from new store
      var ctx1 = new OperationContext(app); // DataSourceName=Default
      // The second context will point to another database
      var ctx2 = new OperationContext(app);
      ctx2.DataSourceName = Books2;
      var session = ctx2.OpenSession();
      var books = session.EntitySet<IBook>().ToList();
      Assert.AreEqual(0, books.Count, "Expected no books in new store");
      //Let's create a pub and a book in VitaBooks2
      var newPub = session.NewPublisher("VBooks");
      var newBook = session.NewBook(BookEdition.Hardcover, BookCategory.Programming, "Voodoo Programming", "Voodoo science in code", newPub, DateTime.Now, 10m);
      session.SaveChanges(); 

      //check we inserted them in VitaBooks2
      session = ctx2.OpenSession();
      var pubCopy = session.EntitySet<IPublisher>().FirstOrDefault(p => p.Name == newPub.Name);
      Assert.IsNotNull(pubCopy, "Publisher not found in VitaBooks2");
      var bookCopy = session.EntitySet<IBook>().FirstOrDefault(b => b.Title == newBook.Title);
      Assert.IsNotNull(bookCopy, "Book not found.");

      //Check that it does not exist in 'main' VitaBooks database
      session = ctx1.OpenSession(); //open session in VitaBooks
      var pub = session.EntitySet<IPublisher>().FirstOrDefault(p => p.Name == newPub.Name);
      Assert.IsNull(pub, "Publisher should not exist in VitaBooks");
      var book = session.EntitySet<IBook>().FirstOrDefault(b => b.Title == newBook.Title);
      Assert.IsNull(book, "Book should not exist in VitaBooks.");

      //Verify both data sources share the DbModel object
      var dsService = app.DataAccess;
      var ds1 = dsService.GetDataSource(ctx1);
      var ds2 = dsService.GetDataSource(ctx2);
      Assert.IsNotNull(ds1, "Default data source not found.");
      Assert.IsNotNull(ds2, "Books2 data source not found.");
      Assert.AreEqual(ds1.Database.DbModel, ds2.Database.DbModel, "Db models are not shared.");
    }


  }//class
}
