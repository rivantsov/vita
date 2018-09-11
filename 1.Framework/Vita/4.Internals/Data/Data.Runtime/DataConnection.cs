using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Vita.Entities;
using Vita.Data.Driver;
using Vita.Entities.Utilities;
using Vita.Entities.Runtime;

namespace Vita.Data.Runtime {

  /// <summary>
  /// A wrapper class for IDbConnection, IDbTransaction objects and some utility methods.
  /// </summary>
  public partial class DataConnection: IDisposable {
    public EntitySession Session;
    public DbSettings DbSettings; 
    public IDataReader ActiveReader; // holds an open reader that uses this connection. Allows application to automatically close the reader when connection is closed/reopened 

    public IDbConnection DbConnection { get; private set; }
    public IDbTransaction DbTransaction { get; private set; }
    public DbConnectionFlags Flags;
    public DbConnectionLifetime Lifetime; 

    private long _transactionStart;

    public DataConnection(EntitySession session, DbSettings settings, DbConnectionLifetime lifetime, bool admin = false) {
      Session = session;
      DbSettings = settings;
      Lifetime = lifetime;
      var connString = admin ? settings.SchemaManagementConnectionString : settings.ConnectionString;
      DbConnection = settings.Driver.CreateConnection(connString);
    }

    public void Open() {
      try {
        if (ActiveReader != null) {
          ActiveReader.Dispose();
          ActiveReader = null; 
        }
        DbConnection.Open();
      } catch (System.Data.Common.DbException sqlEx) {
        var dex = new DataAccessException(sqlEx);
        dex.SubType = DataAccessException.SubTypeConnectionFailed;
        throw dex; 
      }
    }

    //Close connection if there's no active transaction
    public void Close() {
      Util.Check(DbTransaction == null, "Cannot close connection - it has active transaction associated with it.");
      if (ActiveReader != null)
        ActiveReader.Dispose();
      ActiveReader = null; 
      ActiveReader = null; 
      if (DbConnection != null && DbConnection.State != ConnectionState.Closed)
        DbConnection.Close();
    }

    public void BeginTransaction(bool commitOnSave, IsolationLevel isolationLevel = IsolationLevel.Unspecified) {
      if (DbConnection.State != ConnectionState.Open)
        Open();
      DbTransaction = isolationLevel == IsolationLevel.Unspecified ? 
        DbConnection.BeginTransaction() : DbConnection.BeginTransaction(isolationLevel);
      _transactionStart = Session.Context.App.TimeService.ElapsedMilliseconds;
      //Log begin trans
      if (commitOnSave)
        Flags |= DbConnectionFlags.CommitOnSave;
      this.Session.LogMessage("BEGIN TRANS");
    }

    public void Commit() {
      if (DbTransaction == null)
        return; 
      DbTransaction.Commit();
      DbTransaction = null;
      Flags &= ~DbConnectionFlags.CommitOnSave; 
      var now = Session.Context.App.TimeService.ElapsedMilliseconds; 
      //Log Commit trans
      var msg = string.Format("{0} -- {1} ms", "COMMIT TRANS", now - _transactionStart);
      this.Session.LogMessage(msg);
    }

    public void Abort() {
      try {
        if(DbConnection != null && DbConnection.State == ConnectionState.Open && DbTransaction != null)
          DbTransaction.Rollback();
        this.Session.LogMessage("--ROLLBACK TRANS");
      } catch(Exception ex) {
        this.Session.LogMessage("!!!! ROLLBACK TRANS failed: " + ex.Message);
      } finally {
        DbTransaction = null;
        Flags &= ~DbConnectionFlags.CommitOnSave;
      }
    }

    public void Dispose() {
      if (DbTransaction != null)
        Abort();
      Close();
    }


  } //class
}
