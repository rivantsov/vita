using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Samples.BookStore;
using Vita.Data.Driver;
using Vita.Tools.Testing; 

namespace Vita.Testing.ExtendedTests {


  [TestClass]
  public class DirectDbTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      Startup.TearDown(); 
    }


    [TestMethod]
    public void TestDirectDbAccess() {
      Startup.BooksApp.LogTestStart();

      //Note: we need to use entity that is not cached, otherwise test does not work if cache is enabled. 
      // If an entity is cached and we change entity in the database through direct access, 
      // cache does not know about update, so it keeps stale value. We use IBookOrderLine which is not cached. 
      var app = Startup.BooksApp;
      var session = app.OpenSystemSession();

      var directDb = session.GetDirectDbConnector();

      directDb.OpenConnection(); 
      //From this moment, the connection remains open and associated with the entity session
      // let's open transaction
      directDb.BeginTransaction(); 

      // get cs book and remember its price 
      var csBook = session.EntitySet<IBook>().First(b => b.Title.StartsWith("c#"));
      var oldPrice = csBook.Price;

      //Let's add $1 to book prices twice - once thru entity, and another time thru direct SQL
      csBook.Price += 1; 
      session.SaveChanges(); //this will not commit trans

      //Now update price, using direct SQL command
      var cmd = directDb.DbConnection.CreateCommand();
      cmd.Connection = directDb.DbConnection;
      cmd.Transaction = directDb.DbTransaction;
      var prm = cmd.CreateParameter();
      switch(Startup.ServerType) {
        case DbServerType.SQLite:
          // SqlLite does not have schemas; 
          // For SQLite add default DbOptions flag AddSchemaToTableName, so table name is books_Book
          cmd.CommandText = "UPDATE \"books_Book\" SET \"Price\" = \"Price\" + 1 WHERE \"Id\" = @P1;";
          prm.ParameterName = "@P1";
          prm.Value = csBook.Id;
          break;
        case DbServerType.Oracle:
          cmd.CommandText = "UPDATE \"books\".\"Book\" SET \"Price\" = \"Price\" + 1 WHERE \"Id\" = :p;";
          prm.ParameterName = "p";
          prm.Value = csBook.Id.ToByteArray();
          break;
        default:
          cmd.CommandText = "UPDATE \"books\".\"Book\" SET \"Price\" = \"Price\" + 1 WHERE \"Id\" = @P1;";
          prm.ParameterName = "@P1";
          prm.Value = csBook.Id;
          break; 
      }
      cmd.Parameters.Add(prm);
      session.LogMessage("-------------------- Executing direct SQL statement -------------------------");
      // We could call cmd.ExecuteNonQuery() here, but using ExecuteDbCommand method provides automatic logging of the executed command
      // - see _vitaBooks.log file
      directDb.ExecuteNonQuery(cmd);

      directDb.Commit();
      directDb.CloseConnection(); 

      //Check that session.CurrentConnection is null 
      var entSession = (Vita.Entities.Runtime.EntitySession)session;
      Assert.IsNull(entSession.CurrentConnection, "CurrentConnection should be null.");

      //Verify that everything went ok and price changed
      session = app.OpenSystemSession();
      csBook = session.GetEntity<IBook>(csBook.Id);
      Assert.AreEqual((double)oldPrice + 2, (double)csBook.Price, 0.01, "Price did not change");

    }

  }//class
}
