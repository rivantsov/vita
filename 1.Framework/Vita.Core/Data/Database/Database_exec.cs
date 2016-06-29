using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data {

  public partial class Database {

    public object ExecuteDbCommand(IDbCommand command, DataConnection connection, DbExecutionType executionType,
                                    Func<IDataReader, int> resultsReader = null) {
      object result;
      IDataReader reader = null;
      connection.Session.SetLastCommand(command); 
      try {
        command.Connection = connection.DbConnection;
        command.Transaction = connection.DbTransaction;
        var start = CurrentTickCount;
        int recordCount = -1; 
        result = _driver.ExecuteCommand(command, executionType);
        if(executionType == DbExecutionType.Reader) {
          reader = (IDataReader)result;
          if(resultsReader != null)
            recordCount = resultsReader(reader); 
          reader.Close();
        }
        _driver.CommandExecuted(connection, command, executionType);
        var end = CurrentTickCount;
        var timeMs = (int)(end - start);
        LogCommand(connection.Session, command, timeMs, recordCount);
        return result;
      } catch(Exception ex) {
        // Important: in some cases exception on invalid SQL is not thrown immediately but is thrown later when we try to read the results
        // ex (MS SQL): WHERE "Name" LIKE 'ABC%' ESCAPE '' - with empty ESCAPE arg string.
        // So driver.ExecuteDbCommand does NOT catch it, it is thrown only here in call to resultsReader
        var dex = ex as DataAccessException;
        if(dex == null) 
          dex = _driver.ConvertToDataAccessException(ex, command);
        if (connection.DbTransaction != null) {
          connection.Session.LogMessage(" -- Aborting transaction on error");
          connection.Abort();
        }
        connection.Session.LogMessage(" -- Failed command text: ");
        LogCommand(connection.Session, command, 0);
        ReviewExceptionAndAddInfo(dex);
        LogException(connection.Session, dex);
        if(reader != null)
          reader.Close();
        throw dex;
      } finally {
        command.Transaction = null;
        command.Connection = null; 
      }
    }//method

    DataConnection IDataStore.GetConnection(EntitySession session, bool admin) {
      return GetConnection(session, ConnectionLifetime.Operation, admin: admin);
    }
    // Creates new connection or gets previously used from the session
    public DataConnection GetConnection(EntitySession session, 
                  ConnectionLifetime minLifetime = ConnectionLifetime.Operation, bool admin = false) {
      if (admin)
        return new DataConnection(session, this, minLifetime, true);
      var conn = session.CurrentConnection;
      if(conn != null) {
        if (conn.Lifetime < minLifetime)
          conn.Lifetime = minLifetime;
        return conn;
      }
      if (session.Context.DbConnectionMode == DbConnectionReuseMode.KeepOpen)
        minLifetime = ConnectionLifetime.Explicit;
      conn = new DataConnection(session, this, minLifetime, admin: admin);
      session.CurrentConnection = conn; //it will register it in disposables
      return conn; 
    }

    private void ReleaseConnection(DataConnection connection, bool inError = false) {
      if(connection == null)
        return;
      //close reader if exist
      if (connection.ActiveReader != null) {
        connection.ActiveReader.Close();
        connection.ActiveReader = null;
      }
      if (inError) {
        if (connection.DbTransaction != null)
          connection.Abort();
        connection.Close();
        return; 
      }
      //Do not close if there's a transaction, or it is a long-living connection
      if(connection.DbTransaction != null || connection.Lifetime == ConnectionLifetime.Explicit)
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

    public object GetSequenceNextValue(EntitySession session, SequenceDefinition sequence) {
      var dbSeq = this.DbModel.LookupDbObject<DbSequenceInfo>(sequence, throwNotFound: true);
      var conn = GetConnection(session);
      try {
        var dbCmd = this.CreateDbCommand(dbSeq.GetNextValueCommand, conn);
        var result = ExecuteDbCommand(dbCmd, conn, DbExecutionType.Scalar);
        ReleaseConnection(conn); 
        return result; 
      } catch {
        ReleaseConnection(conn, inError: true);
        throw; 
      }      
    }

    #region DbCommand setup
    private IDbCommand CreateDbCommand(DbCommandInfo commandInfo, DataConnection connection) {
      var cmd = connection.DbConnection.CreateCommand();
      cmd.Transaction = connection.DbTransaction;
      connection.Session.SetLastCommand(cmd);
      bool isSp = commandInfo.CommandType == CommandType.StoredProcedure && !connection.Session.Options.IsSet(EntitySessionOptions.DisableStoredProcs);
      if(isSp) {
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = commandInfo.FullCommandName;
      } else {
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = commandInfo.Sql;
      }
      //Create parameters collection
      if(commandInfo.IsTemplatedSql) {

      } else {
        for(int index = 0; index < commandInfo.Parameters.Count; index++)
          _driver.AddParameter(cmd, commandInfo.Parameters[index]);
      }
      return cmd;
    }//method

    // Sets parameter values.
    private void SetCommandParameterValues(DbCommandInfo commandInfo, IDbCommand command, object[] args) {
      for(int i = 0; i < commandInfo.Parameters.Count; i++) {
        var prmInfo = commandInfo.Parameters[i];
        if(prmInfo.ArgIndex >= 0) {
          var prm = (IDbDataParameter)command.Parameters[i];
          var v = args[prmInfo.ArgIndex];
          var conv = prmInfo.TypeInfo.PropertyToColumnConverter;
          if(v != null && conv != null)
            v = conv(v);
          prm.Value = v;
        }
      } //for i
    }

    private void FormatTemplatedSql(DbCommandInfo commandInfo, IDbCommand command, object[] args) {
      if(args == null || args.Length == 0)
        return;
      var values = new string[args.Length];
      for(int i = 0; i < commandInfo.Parameters.Count; i++) {
        var prmInfo = commandInfo.Parameters[i];
        if(prmInfo.ArgIndex >= 0) {
          var v = args[prmInfo.ArgIndex];
          var strV = prmInfo.ToLiteralConverter(v);
          values[i] = strV;
        }
        command.CommandText = string.Format(commandInfo.Sql, values);
      } //for i
    }
    #endregion

    #region Logging

    private void LogCommand(EntitySession session, IDbCommand command, long executionTime, int rowCount = -1) {
      if (session.LogDisabled)
        return; 
      var entry = new DbCommandLogEntry(session.Context, command, DbModel.Driver.CommandCallFormat, _timeService.UtcNow, executionTime, rowCount);
      session.AddLogEntry(entry);
    }

    protected void LogComment(EntitySession session, string comment, params object[] args) {
      if (session.LogDisabled)
        return;
      var entry = new InfoLogEntry(session.Context, comment, args);
      session.AddLogEntry(entry);
    }

    protected void LogException(EntitySession session, Exception ex) {
      var entry = new ErrorLogEntry(session.Context, ex);
      session.AddLogEntry(entry);
    }

    #endregion


  }//class
}
