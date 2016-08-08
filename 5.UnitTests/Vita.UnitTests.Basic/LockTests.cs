using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using Vita.Common; 
using Vita.Entities;
using Vita.Entities.Locking;
using System.IO;

namespace Vita.UnitTests.Basic.LockTests {

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
      var dbName = (string)dbConn.ExecuteDbCommand(cmdGetDb, Data.DbExecutionType.Scalar);
      var cmd = dbConn.DbConnection.CreateCommand();
      //Note: for real-wold databases, when you enable these options, you need to switch to single-user mode
      // temporarily; otherwise the statement might hang trying to lock database
      cmd.CommandText = string.Format(@"
ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
ALTER DATABASE {0} SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE {0} SET READ_COMMITTED_SNAPSHOT OFF;
ALTER DATABASE {0} SET MULTI_USER;
", dbName);
      dbConn.ExecuteDbCommand(cmd, Data.DbExecutionType.NonQuery);
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
    int _updateErrorCount;
    int _sumErrorCount; 

    [TestCleanup]
    public void TestCleanup() {
      if (_app != null) {
        _app.Flush();
        _app.Shutdown();
      }
    }


    [TestMethod]
    public void TestLocking() {
      switch (SetupHelper.ServerType) {
        case Data.Driver.DbServerType.SqlCe:
        case Data.Driver.DbServerType.Sqlite: 
          return; //do not support locks
      }
      if (File.Exists(_logFileName))
        File.Delete(_logFileName);
      SetupHelper.DropSchemaObjects("lck");
      _app = new LockTestApp();
      SetupHelper.ActivateApp(_app);

      //Run with locks - we should see no errors
      RunTests(LockOptions.SharedRead, LockOptions.ForUpdate, 30, 50);
      Assert.AreEqual(0, _updateErrorCount, "Expected no update errors");
      Assert.AreEqual(0, _sumErrorCount, "Expected no sum errors");
      
      //Uncomment to see errros when run without locks; commented to save time of running unit tests
      /*  
      //Run without locks - we should see multiple errors; using more threads to get errors for sure
      RunTests(LockOptions.None, LockOptions.None, 40, 50);
      var note = " Note: this can happen occasionally, ignore.";
      Assert.IsTrue(_updateErrorCount > 0, "Expected some update errors." + note);
      Assert.IsTrue(_sumErrorCount > 0, "Expected some sum errors." + note);
      */
    }//method

    private void RunTests(LockOptions readOptions, LockOptions writeOptions, int threadCount = 10, int readWriteCount = 50) {
      // delete old data and create 5 docs
      SetupHelper.DeleteAll(_app);
      var session = _app.OpenSession();
      for (int i = 0; i < 5; i++) {
        var iDoc = session.NewEntity<IDoc>();
        iDoc.Name = "D" + i;
      }
      session.SaveChanges();
      var docIds = session.GetEntities<IDoc>(take: 10).Select(d => d.Id).ToArray(); 

      // clear error counts, 
      _updateErrorCount = 0;
      _sumErrorCount = 0; 
      // create multiple threads and start reading/writing
      Task[] tasks = new Task[threadCount];
      for(int i = 0; i < threadCount; i++) {
        tasks[i] = Task.Run(() => RunRandomReadWriteOp(docIds, readOptions, writeOptions, readWriteCount));
      }
      Task.WaitAll(tasks);
    }

    private void RunRandomReadWriteOp(Guid[] docIds, LockOptions readOptions, LockOptions writeOptions, int readWriteCount) {
      var rand = new Random(Thread.CurrentThread.ManagedThreadId);
      IEntitySession session = null; 
      for(int i = 0; i < readWriteCount; i++) {
        var randomDocId = docIds[rand.Next(docIds.Length)];
        var randomValueName = "N" + rand.Next(5);
        IDoc iDoc; 
        IDocDetail iDet;
        int randomOp = -1;
        try {
          Thread.Yield();
          session = _app.OpenSession();
          randomOp = rand.Next(5); 
          switch (randomOp) {
            case 0: case 1: //insert/update, we give it an edge over deletes, to have 2 upserts for 1 delete
              session.LogMessage("\r\n----------------- Update/insert value ---------------------");
              iDoc = session.GetEntity<IDoc>(randomDocId, writeOptions);
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
              
              var entSession = (Vita.Entities.Runtime.EntitySession)session;
              if (entSession.CurrentConnection != null)
                Debugger.Break(); //check that connection is closed and removed from session
              break;

            case 2: //delete if exists
              session.LogMessage("\r\n----------------- Delete value ---------------------");
              //Note: deletes do not throw any errors - if record does not exist (had been just deleted), stored proc do not throw error
              iDoc = session.GetEntity<IDoc>(randomDocId, writeOptions);
              Thread.Yield(); //allow others mess up
              iDet = iDoc.Details.FirstOrDefault(iv => iv.Name == randomValueName);
              if (iDet != null) {
                session.DeleteEntity(iDet);
                iDoc.Details.Remove(iDet); 
                iDoc.Total = iDoc.Details.Sum(v => v.Value);
              } 
              Thread.Yield(); 
              session.SaveChanges(); // even if there's no changes, it will release lock
              break;

            case 3: case 4: //read and check total
              session.LogMessage("\r\n----------------- Loading doc ---------------------");
              iDoc = session.GetEntity<IDoc>(randomDocId, readOptions);
              Thread.Yield(); //to let other thread mess it up
              var valuesSum = iDoc.Details.Sum(v => v.Value);
              if (valuesSum != iDoc.Total) {
                session.LogMessage("!!!! Sum error: Doc.Total: {0}, Sum(Value): {1}", iDoc.Total, valuesSum);
                Interlocked.Increment(ref _sumErrorCount);
              }
              Thread.Yield(); 
              session.ReleaseLocks(); 
              break;
          }//switch
          WriteLog(session); 
        } catch (Exception ex) { //most will be UniqueIndexViolation
          Debug.WriteLine(ex.ToLogString());
          System.Threading.Interlocked.Increment(ref _updateErrorCount);
          if (session != null) {
            WriteLog(session); 
            var log = session.Context.LocalLog.GetAllAsText();
            Debug.WriteLine(log);
            var entSession = (Vita.Entities.Runtime.EntitySession)session;
            if (entSession.CurrentConnection != null)
              entSession.CurrentConnection.Close(); 
          }
        }
      }//for i
    }//method

    // We use a separate log file; with multiple threads log entries in regular log file will be mixed, so we use a separate  
    // file to log combined entries per session/thread
    string _logFileName = "__locksTest.log";
    static object _lock = new object(); 
    private void WriteLog(IEntitySession session) {
      var log = Environment.NewLine + session.Context.LocalLog.GetAllAsText() + Environment.NewLine;
      lock (_lock)
        File.AppendAllText(_logFileName, log);
    }
  } //class
}// ns
