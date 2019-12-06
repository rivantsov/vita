using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using Vita.Entities;
using Vita.Entities.Locking;
using System.IO;
using Vita.Entities.Services;
using Vita.Entities.Logging;

namespace Vita.Testing.BasicTests.Locks {

  /*
   This test is similar to the sample app for the CodeProject article: 
       https://www.codeproject.com/Articles/1117051/Using-locks-to-manage-concurrent-access-to-compoun
   This unit test does similar heavy concurrent hitting on two tables using VITA entities and VITA locking API. 
   (unlike the code project article which uses VITA-independent code). 
   Read the article or the repo page here:
       https://github.com/rivantsov/LockDemo
   to get some explanations what the test is doing. 
   Short description: we create 5 db 'docs', with header-details structure, and then hit it on multiple parallel threads with multiple read/write commands, while 
   checking on every operation that document integrity is OK. 
   */

  [Entity]
  public interface IDoc {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Size(30)]
    string Name { get; set; }
    IList<IDocDetail> Details {get;}
    int Total {get; set;} //total of all values
  }

  [Entity, Unique("Doc,Name")]
  public interface IDocDetail {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    IDoc Doc { get; set; }

    [Size(30)]
    string Name { get; set; }
    int Value { get; set; }
  }

  public class LockTestModule : EntityModule {
    public LockTestModule(EntityArea area)  : base(area, "LockTestModule", version: new Version("1.0.0.0")) {
      RegisterEntities(typeof(IDoc), typeof(IDocDetail));
    }
    public override void RegisterMigrations(Data.Upgrades.DbMigrationSet migrations) {
      // MS SQL - special case; we have to use Snapshot isolation level for read transaction. 
      // For this, we have to explicitly enable it for the database. We do it using migration 
      if (migrations.ServerType == Data.Driver.DbServerType.MsSql)
        migrations.AddPostUpgradeAction("1.0.0.0", "EnableSnapshot", "Enable snapshot isolation", EnableSnapshotIsolation);
    }//method

    private static void EnableSnapshotIsolation(IEntitySession session) {
      var dbConn = session.GetDirectDbConnector(admin: true);
      dbConn.OpenConnection();
      // First get current DB name
      var cmdGetDb = dbConn.DbConnection.CreateCommand();
      cmdGetDb.CommandText = "SELECT DB_NAME()";
      var dbName = dbConn.ExecuteScalar<string>(cmdGetDb);
      var cmd = dbConn.DbConnection.CreateCommand();
      //Note: for real-wold databases, when you enable these options, you need to switch to single-user mode
      // temporarily; otherwise the statement might hang trying to lock database
      cmd.CommandText = string.Format(@"
ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
ALTER DATABASE {0} SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE {0} SET READ_COMMITTED_SNAPSHOT OFF;
ALTER DATABASE {0} SET MULTI_USER;
", dbName);
      dbConn.ExecuteNonQuery(cmd);
      dbConn.CloseConnection();
    }


  }//class

  // We skip defining custom entity module and use base EntityModule class
  public class LockTestApp : EntityApp {
    public LockTestApp() {
      var area = AddArea("lck");
      var mainModule = new LockTestModule(area);
    }
  }//class


  [TestClass]
  public class LockTests {
    LockTestApp _app;
    // We use a separate log file; with multiple threads log entries in regular log file will be mixed, so we use a separate  
    // file to log combined entries per session/thread
    string _logFileName = "__locksTest.log";

    int _updateErrorCount;
    int _sumErrorCount;

    [TestCleanup]
    public void TestCleanup() {
      if(_app != null) {
        _app.Flush();
        _app.Shutdown();
      }
    }


    [TestMethod]
    public void TestLocking() {

      switch(Startup.ServerType) {
        case Data.Driver.DbServerType.SQLite:
          return; //do not support locks
      }
      if(File.Exists(_logFileName))
        File.Delete(_logFileName);
      _app = new LockTestApp();
      _app.LogPath = _logFileName;
      Startup.ActivateApp(_app);

      //Run with locks - we should see no errors
      int ThreadCount = 30; 
      int UpdateCount = 50;
      RunTests(LockType.SharedRead, LockType.ForUpdate, ThreadCount, UpdateCount);
      Assert.AreEqual(0, _updateErrorCount, "Expected no update errors");
      Assert.AreEqual(0, _sumErrorCount, "Expected no sum errors");
      Trace.WriteLine("Lock test completed");

      //Uncomment to see errros when run without locks; commented to save time of running unit tests
      /*   
      //Run without locks - we should see multiple errors; 
      RunTests(LockType.None, LockType.None, 60, 50);
      var note = " Note: this can happen occasionally, ignore.";
      Assert.IsTrue(_updateErrorCount > 0, "Expected some update errors." + note);
      Assert.IsTrue(_sumErrorCount > 0, "Expected some sum errors." + note);
      */
    }//method

    private void RunTests(LockType readLock, LockType writeLock, int threadCount = 10, int readWriteCount = 50) {
      // delete old data and create 5 docs
      Startup.DeleteAll(_app);
      var session = _app.OpenSession();
      for(int i = 0; i < 5; i++) {
        var iDoc = session.NewEntity<IDoc>();
        iDoc.Name = "D" + i;
      }
      session.SaveChanges();

      session = _app.OpenSession();
      var docIds = session.GetEntities<IDoc>(take: 10).Select(d => d.Id).ToArray();

      //quick test to review dbcommand with lock
      var docId0 = docIds[0];
      var doc0 = session.GetEntity<IDoc>(docId0, LockType.ForUpdate);
      session.ReleaseLocks(); 
      

      // clear error counts, 
      _updateErrorCount = 0;
      _sumErrorCount = 0;
      // create multiple threads and start reading/writing
      Task[] tasks = new Task[threadCount];
      for(int i = 0; i < threadCount; i++) {
        tasks[i] = Task.Run(() => RunRandomReadWriteOp(docIds, readLock, writeLock, readWriteCount));
      }
      Task.WaitAll(tasks);
      _app.Flush();
    }

    private void RunRandomReadWriteOp (Guid[] docIds, LockType readLock, LockType writeLock, int readWriteCount) {
      var rand = new Random(Thread.CurrentThread.ManagedThreadId);
      IEntitySession session = null;
      // Use context with its own buffered op log - all entries related to single load/update operation are buffered, 
      // and then flushed together at the end of loop body; so they will appear together in the output file
      var ctx = new OperationContext(_app);
      ctx.Log = new BufferedLog(ctx.LogContext);

      for(int i = 0; i < readWriteCount; i++) {
        session = ctx.OpenSession();
        var randomDocId = docIds[rand.Next(docIds.Length)];
        IDoc iDoc; 
        IDocDetail iDet;
        int randomOp = -1;
        try {
          Thread.Yield();
          var randomValueName = "N" + rand.Next(5);
          randomOp = rand.Next(7); 
          switch (randomOp) {
            case 0: case 1: //read and check total
              session.LogMessage("\r\n----------------- Load, check Doc total ---------------------");
              iDoc = session.GetEntity<IDoc>(randomDocId, readLock);
              Thread.Yield(); //to let other thread mess it up
              var valuesSum = iDoc.Details.Sum(v => v.Value);
              if(valuesSum != iDoc.Total) {
                session.LogMessage("!!!! Sum error: Doc.Total: {0}, Sum(Value): {1}", iDoc.Total, valuesSum);
                Interlocked.Increment(ref _sumErrorCount);
              }
              Thread.Yield();
              session.ReleaseLocks();
              session.LogMessage("\r\n-------------- Completed Load/check total ---------------------");
              break;

            case 2: case 3: case 4: 
              //insert/update, we give it an edge over deletes, to have 2 upserts for 1 delete
              session.LogMessage("\r\n----------------- Update/insert DocDetail ---------------------");
              iDoc = session.GetEntity<IDoc>(randomDocId, writeLock);
              Thread.Yield();
              iDet = iDoc.Details.FirstOrDefault(iv => iv.Name == randomValueName);
              if (iDet == null) {
                iDet = session.NewEntity<IDocDetail>();
                iDet.Doc = iDoc;
                iDet.Name = randomValueName;
                iDoc.Details.Add(iDet); //to correctly calculate total
              } 
              iDet.Value = rand.Next(10);
              iDoc.Total = iDoc.Details.Sum(v => v.Value);
              Thread.Yield(); 
              session.SaveChanges();
              session.LogMessage("\r\n------Completed Update/insert doc detail ---------------------");

              var entSession = (Vita.Entities.Runtime.EntitySession)session;
              if (entSession.CurrentConnection != null)
                Debugger.Break(); //check that connection is closed and removed from session
              break;

            case 6: //delete if exists
              session.LogMessage("\r\n----------------- Delete doc detail ---------------------");
              //Note: deletes do not throw any errors - if record does not exist (had been just deleted), stored proc do not throw error
              iDoc = session.GetEntity<IDoc>(randomDocId, writeLock);
              Thread.Yield(); //allow others mess up
              iDet = iDoc.Details.FirstOrDefault(iv => iv.Name == randomValueName);
              if(iDet != null) {
                session.DeleteEntity(iDet);
                iDoc.Details.Remove(iDet);
                iDoc.Total = iDoc.Details.Sum(v => v.Value);
                session.SaveChanges(); // even if there's no changes, it will release lock
              } else
                session.ReleaseLocks();
              session.LogMessage("\r\n----------------- Completed delete doc detail ---------------------");
              break;

          }//switch
        } catch (Exception ex) { //most will be UniqueIndexViolation
          Debug.WriteLine(ex.ToLogString());
          System.Threading.Interlocked.Increment(ref _updateErrorCount);
          session.Context.Log.AddEntry(new ErrorLogEntry(ctx.LogContext, ex));
          var entSession = (Vita.Entities.Runtime.EntitySession)session;
          if (entSession.CurrentConnection != null)
            entSession.CurrentConnection.Close();
          //session.Context.Log.Flush();
          _app.Flush(); 
        } finally {
          //_app.Flush();
        }
        //session.Context.Log.Flush();
      }//for i
    }//method

  } //class
}// ns
