using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Logging;
using Vita.Entities.Locking;

using Vita.Data.Sql;

namespace Vita.Data.Runtime {

  public partial class Database {

    public void ExecuteDataCommand(DataCommand command) {
      var conn = command.Connection;
      var dbCommand = command.DbCommand; 
      conn.Session.SetLastCommand(dbCommand); 
      try {
        dbCommand.Connection = conn.DbConnection;
        dbCommand.Transaction = conn.DbTransaction;
        var start = _timeService.ElapsedMilliseconds;
        command.Result = _driver.ExecuteCommand(dbCommand, command.ExecutionType);
        conn.ActiveReader = command.Result as IDataReader; // if it is reader, save it in connection
        command.ProcessedResult = (command.ResultProcessor == null) 
                                   ? command.Result 
                                   : command.ResultProcessor.ProcessResult(command);
        _driver.CommandExecuted(conn, dbCommand, command.ExecutionType);
        ProcessOutputCommandParams(command);
        var end = _timeService.ElapsedMilliseconds;
        command.TimeMs = (int)(end - start);
        LogCommand(conn.Session, dbCommand, command.TimeMs, command.RowCount);
      } catch(Exception ex) {
        // Important: in some cases exception on invalid SQL is not thrown immediately but is thrown later when we try to read the results
        // ex (MS SQL): WHERE "Name" LIKE 'ABC%' ESCAPE '' - with empty ESCAPE arg string.
        // So driver.ExecuteDbCommand does NOT catch it, it is thrown only here in call to resultsReader
        var dex = ex as DataAccessException;
        if(dex == null) 
          dex = _driver.ConvertToDataAccessException(ex, dbCommand);
        if (conn.DbTransaction != null) {
          conn.Session.LogMessage(" -- Aborting transaction on error");
          conn.Abort();
        }
        conn.Session.LogMessage(" -- Failed command text: ");
        LogCommand(conn.Session, dbCommand, 0);
        ReviewExceptionAndAddInfo(dex);
        LogException(conn.Session, dex);
        if(conn.ActiveReader != null)
          conn.ActiveReader.Dispose();
        throw dex;
      } finally {
        dbCommand.Transaction = null;
        dbCommand.Connection = null;
        if (conn.ActiveReader != null) {
          conn.ActiveReader.Close(); 
          conn.ActiveReader = null;
        }
      }
    }//method

    private void ProcessOutputCommandParams(DataCommand command) {
      foreach(var rec in command.Records) {
        if(rec.DbCommandData == null)
          continue;
        foreach(var prmInfo in rec.DbCommandData.OutputParameters) {
          var value = prmInfo.Column.Converter.ColumnToProperty(prmInfo.Parameter.Value);
          rec.SetValueDirect(prmInfo.Column.Member, value);
        }
      }
    }


    // Creates new connection or gets previously used from the session
    public DataConnection GetConnection(EntitySession session, 
                  DbConnectionLifetime connLifetime = DbConnectionLifetime.Operation, bool admin = false) {
      if (admin)
        return new DataConnection(session, this.Settings, connLifetime, true);
      var conn = session.CurrentConnection;
      if(conn != null) {
        if (conn.Lifetime < connLifetime)
          conn.Lifetime = connLifetime;
        return conn;
      }
      // if we have KeepOpen mode (recommended in Web app), then make Lifetime explicit -
      // i.e. until explicitly called to close; web call context handler will do it after completing 
      // processing requests
      if (session.Context.DbConnectionMode == DbConnectionReuseMode.KeepOpen)
        connLifetime = DbConnectionLifetime.Explicit;
      conn = new DataConnection(session, this.Settings, connLifetime, admin: admin);
      session.CurrentConnection = conn; //it will register it in disposables
      return conn; 
    }

    //we need to check if we require transaction (if we are setting LOCK)
    private DataConnection GetConnectionWithLock(EntitySession session, LockType lockType) {
      var conn = GetConnection(session);
      var hasLock = lockType == LockType.ForUpdate || lockType == LockType.SharedRead;
      if (conn.DbTransaction == null && hasLock) {
        //We need to start transaction with proper isolation level. We also need to associate connection with session, 
        // so it will stay there and will be reused for all coming operations
        if (conn.Lifetime < DbConnectionLifetime.Transaction)
          conn.Lifetime = DbConnectionLifetime.Transaction;
        session.CurrentConnection = conn;
        var isoLevel = _driver.GetIsolationLevel(lockType);
        conn.BeginTransaction(commitOnSave: true, isolationLevel: isoLevel);
      }
      return conn;
    }


    private void ReleaseConnection(DataConnection connection, bool inError = false) {
      if(connection == null)
        return;
      //close reader if exist
      if (connection.ActiveReader != null) {
        connection.ActiveReader.Dispose();
        connection.ActiveReader = null;
      }
      if (inError) {
        if (connection.DbTransaction != null)
          connection.Abort();
        connection.Close();
        return; 
      }
      //Do not close if there's a transaction, or it is a long-living connection
      if(connection.DbTransaction != null || connection.Lifetime == DbConnectionLifetime.Explicit)
        return; 
      //Otherwise, close it
      connection.Close();
      if (connection.Session.CurrentConnection == connection)
        connection.Session.CurrentConnection = null; 
    }

    //Inspect specific exception types and add information to exception
    private void ReviewExceptionAndAddInfo(Exception exception) {
      var dex = exception as DataAccessException;
      if (dex == null)
        return;
      switch (dex.SubType) {
        case DataAccessException.SubTypeUniqueIndexViolation:
          //We need to add entity name and member names to the exception
          var dbKeyName = dex.Data[DataAccessException.KeyDbKeyName] as string;
          if (dbKeyName == null)
            return;
          var dbKey = DbModel.FindKey(dbKeyName);
          if (dbKey != null) {
            var entKey = dbKey.EntityKey;
            dex.Data[DataAccessException.KeyEntityName] = entKey.Entity.Name;
            dex.Data[DataAccessException.KeyEntityKeyName] = entKey.Name;
            dex.Data[DataAccessException.KeyIndexAlias] = entKey.Alias;
            dex.Data[DataAccessException.KeyMemberNames] = entKey.GetMemberNames(",");
          }
          return;
        case DataAccessException.SubTypeConcurrentUpdate:
          var tableName = dex.Data[DataAccessException.KeyTableName] as string;
          if (tableName == null)
            return;
          var table = DbModel.GetTable(tableName);
          if (table != null)
            dex.Data[DataAccessException.KeyEntityName] = table.Entity.Name;
          return;
      }
    }//method


    #region Logging

    private void LogCommand(EntitySession session, IDbCommand command, long executionTime, int rowCount = -1) {
      if (!session.LogEnabled)
        return; 
      session.Log.LogDbCommand(command, executionTime, rowCount);
    }

    protected void LogComment(EntitySession session, string comment, params object[] args) {
      if (!session.LogEnabled)
        return;
      var entry = new InfoLogEntry(session.Context, comment, args);
      session.AddLogEntry(entry);
    }

    protected void LogException(EntitySession session, Exception ex) {
      if(!session.LogEnabled)
        return;
      var entry = new ErrorLogEntry(session.Context, ex);
      session.AddLogEntry(entry);
    }

    #endregion


    public object ExecuteDirectDbCommand(IDbCommand command, DataConnection connection, DbExecutionType execType) {
      object result;
      connection.Session.SetLastCommand(command);
      try {
        command.Connection = connection.DbConnection;
        command.Transaction = connection.DbTransaction;
        var start = _timeService.ElapsedMilliseconds;
        int recordCount = -1;
        result = _driver.ExecuteCommand(command, execType);
        // if (execType == DbExecutionType.Reader)
        connection.ActiveReader = result as IDataReader; // if it is reader, save it in connection
        _driver.CommandExecuted(connection, command, execType);
        var end = _timeService.ElapsedMilliseconds;
        var timeMs = (int)(end - start);
        LogCommand(connection.Session, command, timeMs, recordCount);
        return result;
      } catch (Exception ex) {
        // Important: in some cases exception on invalid SQL is not thrown immediately but is thrown later when we try to read the results
        // ex (MS SQL): WHERE "Name" LIKE 'ABC%' ESCAPE '' - with empty ESCAPE arg string.
        // So driver.ExecuteDbCommand does NOT catch it, it is thrown only here in call to resultsReader
        var dex = ex as DataAccessException;
        if (dex == null)
          dex = _driver.ConvertToDataAccessException(ex, command);
        if (connection.DbTransaction != null) {
          connection.Session.LogMessage(" -- Aborting transaction on error");
          connection.Abort();
        }
        connection.Session.LogMessage(" -- Failed command text: ");
        LogCommand(connection.Session, command, 0);
        ReviewExceptionAndAddInfo(dex);
        LogException(connection.Session, dex);
        if (connection.ActiveReader != null)
          connection.ActiveReader.Dispose();
        throw dex;
      } finally {
        /* //SQLite does not like this
        command.Transaction = null;
        command.Connection = null;
        connection.ActiveReader = null;
        */
      }
    }//method



  }//class
}
