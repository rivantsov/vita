using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Linq;
using Vita.Entities.Runtime;
using Vita.Entities;
using System.Threading.Tasks;
using System.Data;

namespace Vita.Data.Runtime;

partial class Database {

  public async Task<object> ExecuteLinqCommandAsync (EntitySession session, LinqCommand command) {
    var conn = GetConnectionWithLock(session, command.LockType);
    try {
      object result;
      if (command.Operation == LinqOperation.Select)
        result = await ExecuteLinqSelectAsync(session, command, conn);
      else
        result = await ExecuteLinqNonQueryAsync(session, command, conn);
      ReleaseConnection(conn);
      return result;
    } catch (Exception dex) {
      ReleaseConnection(conn, inError: true);
      dex.AddValue(DataAccessException.KeyLinqQuery, command.ToString());
      throw;
    }
  }

  public async Task<object> ExecuteLinqSelectAsync(EntitySession session, LinqCommand command, DataConnection conn) {
    var sql = SqlFactory.GetLinqSql(command);
    var genMode = command.Options.IsSet(QueryOptions.NoParameters) ?
                           SqlGenMode.NoParameters : SqlGenMode.PreferParam;
    var cmdBuilder = new DataCommandBuilder(this._driver, batchMode: false, mode: genMode);
    cmdBuilder.AddLinqStatement(sql, command.ParamValues);
    var dataCmd = cmdBuilder.CreateCommand(conn, DbExecutionType.Reader, sql.ResultProcessor);
    await ExecuteDataCommandAsync(dataCmd);
    return dataCmd.ProcessedResult;
  }

  private async Task<object> ExecuteLinqNonQueryAsync (EntitySession session, LinqCommand command, DataConnection conn) {
    var sql = SqlFactory.GetLinqSql(command);
    var fmtOptions = command.Options.IsSet(QueryOptions.NoParameters) ?
                           SqlGenMode.NoParameters : SqlGenMode.PreferParam;
    var cmdBuilder = new DataCommandBuilder(this._driver);
    cmdBuilder.AddLinqStatement(sql, command.ParamValues);
    var dataCmd = cmdBuilder.CreateCommand(conn, DbExecutionType.NonQuery, sql.ResultProcessor);
    await ExecuteDataCommandAsync(dataCmd);
    return dataCmd.ProcessedResult ?? dataCmd.Result;
  }

  public async Task ExecuteDataCommandAsync (DataCommand command) {
    var conn = command.Connection;
    var dbCommand = command.DbCommand;
    conn.Session.SetLastCommand(dbCommand);
    try {
      dbCommand.Connection = conn.DbConnection;
      dbCommand.Transaction = conn.DbTransaction;
      var start = Util.GetTimestamp();
      command.Result = await _driver.ExecuteCommandAsync(dbCommand, command.ExecutionType);
      conn.ActiveReader = command.Result as IDataReader; // if it is reader, save it in connection
      command.ProcessedResult = (command.ResultProcessor == null)
                                 ? command.Result
                                 : command.ResultProcessor.ProcessResult(command);
      _driver.CommandExecuted(conn, dbCommand, command.ExecutionType);
      ProcessOutputCommandParams(command);
      command.TimeMs = Util.GetTimeSince(start).TotalMilliseconds;
      LogCommand(conn.Session, dbCommand, command.TimeMs, command.RowCount);
    } catch (Exception ex) {
      // Important: in some cases exception on invalid SQL is not thrown immediately but is thrown later when we try to read the results
      // ex (MS SQL): WHERE "Name" LIKE 'ABC%' ESCAPE '' - with empty ESCAPE arg string.
      // So driver.ExecuteDbCommand does NOT catch it, it is thrown only here in call to resultsReader
      var dex = ex as DataAccessException;
      if (dex == null)
        dex = _driver.ConvertToDataAccessException(ex, dbCommand);
      if (conn.DbTransaction != null) {
        conn.Session.LogMessage(" -- Aborting transaction on error");
        conn.Abort();
      }
      conn.Session.LogMessage(" -- Failed command text: ");
      LogCommand(conn.Session, dbCommand, 0);
      ReviewExceptionAndAddInfo(dex);
      LogException(conn.Session, dex);
      if (conn.ActiveReader != null)
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


}
