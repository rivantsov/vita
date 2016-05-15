using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Vita.Entities;
using Vita.Data.Driver;
using Vita.Common;
using Vita.Entities.Runtime;

namespace Vita.Data {
  [Flags]
  public enum ConnectionFlags {
    None = 0,
    CommitOnSave = 1,
  }

  public enum ConnectionLifetime {
    Operation,
    Transaction,
    Explicit,

  }

  /// <summary>
  /// A wrapper class for IDbConnection, IDbTransaction objects and some utility methods.
  /// </summary>
  public partial class DataConnection : IDirectDbConnector, IDisposable {
    public EntitySession Session;
    public Database Database; 
    public IDataReader ActiveReader; // holds an open reader that uses this connection. Allows application to automatically close the reader when connection is closed/reopened 

    public IDbConnection DbConnection { get; private set; }
    public IDbTransaction DbTransaction { get; private set; }
    public ConnectionFlags Flags;
    public ConnectionLifetime Lifetime; 

    private long _transactionStart;

    public DataConnection(EntitySession session, Database database, ConnectionLifetime lifetime, bool admin = false) {
      Session = session;
      Database = database;
      Lifetime = lifetime;
      var connString = admin ? database.Settings.SchemaManagementConnectionString : database.Settings.ConnectionString;
      DbConnection = database.DbModel.Driver.CreateConnection(connString);
    }

    public void Open() {
      try {
        if (ActiveReader != null) {
          ActiveReader.Close();
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
        ActiveReader.Close();
      ActiveReader = null; 
      if (DbConnection != null && DbConnection.State != ConnectionState.Closed)
        DbConnection.Close();
      Session.CurrentConnection = null; 
    }

    public void BeginTransaction(bool commitOnSave, IsolationLevel isolationLevel = IsolationLevel.Unspecified) {
      if (DbConnection.State != ConnectionState.Open)
        Open();
      DbTransaction = isolationLevel == IsolationLevel.Unspecified ? 
        DbConnection.BeginTransaction() : DbConnection.BeginTransaction(isolationLevel);
      _transactionStart = Session.Context.App.TimeService.ElapsedMilliseconds;
      //Log begin trans
      if (commitOnSave)
        Flags |= ConnectionFlags.CommitOnSave;
      this.Session.LogMessage(this.Database.Settings.Driver.BatchBeginTransaction);
    }

    public void Commit() {
      if (DbTransaction == null)
        return; 
      DbTransaction.Commit();
      DbTransaction = null;
      Flags &= ~ConnectionFlags.CommitOnSave; 
      var now = Session.Context.App.TimeService.ElapsedMilliseconds; 
      //Log Commit trans
      var msg = string.Format("{0} -- {1} ms", this.Database.Settings.Driver.BatchCommitTransaction, now - _transactionStart);
      this.Session.LogMessage(msg);
    }

    public void Abort() {
      try {
        if(DbConnection != null && DbConnection.State == ConnectionState.Open && DbTransaction != null)
          DbTransaction.Rollback();
        DbTransaction = null;
        Flags &= ~ConnectionFlags.CommitOnSave;
        this.Session.LogMessage("--ROLLBACK TRANS");
      } catch { }
    }

    public void Dispose() {
      if (DbTransaction != null)
        Abort();
      Close();
      if (Session.CurrentConnection == this)
        Session.CurrentConnection = null; 
    }

    #region IDirectDbConnector methods
    object IDirectDbConnector.ExecuteDbCommand(IDbCommand command, DbExecutionType executionType, Func<IDataReader, int> resultsReader) {
      return this.Database.ExecuteDbCommand(command, this, executionType, resultsReader);
    }

    void IDirectDbConnector.OpenConnection() {
      this.Open(); 
    }

    void IDirectDbConnector.CloseConnection() {
      this.Close(); 
    }
    #endregion


  } //class
}
