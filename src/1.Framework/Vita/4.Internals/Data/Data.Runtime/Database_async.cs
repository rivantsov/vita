using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Linq;
using Vita.Entities.Runtime;
using Vita.Entities;
using System.Threading.Tasks;
using System.Data;
using Vita.Entities.Model;

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
                                 : await command.ResultProcessor.ProcessResultsAsync(command);
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

  public async Task SaveChangesAsync(EntitySession session) {
    if (session.HasChanges()) {
      var conn = GetConnection(session);
      var updateSet = new DbUpdateSet(session, this.DbModel, conn);
      var batchMode = ShouldUseBatchMode(updateSet);
      if (batchMode)
        await SaveChangesInBatchModeAsync(updateSet);
      else
        await SaveChangesNoBatchAsync(updateSet);
    }
    //commit if we have session connection with transaction and CommitOnSave
    var sConn = session.CurrentConnection;
    if (sConn != null) {
      if (sConn.DbTransaction != null && sConn.Flags.IsSet(DbConnectionFlags.CommitOnSave))
        sConn.Commit();
      if (sConn.Lifetime != DbConnectionLifetime.Explicit)
        ReleaseConnection(sConn);
    }
    session.ScheduledCommandsAtStart = null;
    session.ScheduledCommandsAtEnd = null;
  }

  // Note: scheduled commands are already in batch commands
  private async Task SaveChangesInBatchModeAsync(DbUpdateSet updateSet) {
    var batchBuilder = new DbBatchBuilder(this);
    var batch = batchBuilder.Build(updateSet);
    LogComment(updateSet.Session, "-- BEGIN BATCH ({0} rows, {1} batch command(s)) ---------------------------",
            updateSet.Records.Count, batch.Commands.Count);
    if (batch.Commands.Count == 1) {
      await ExecuteBatchSingleCommandAsync(batch);
    } else {
      await ExecuteBatchMultipleCommandsAsync(batch);
    }


    LogComment(updateSet.Session, "-- END BATCH --------------------------------------\r\n");

    var postExecActions = new List<Action>();

    var session = updateSet.Session;
    //execute post-execute actions; it is usually handling output parameter values
    // Finalize records after update
    foreach (var rec in updateSet.Records) {
      rec.EntityInfo.SaveEvents.OnSubmittedChanges(rec);
    }
  }

  private async Task SaveChangesNoBatchAsync (DbUpdateSet updateSet) {
    var session = updateSet.Session;
    var conn = updateSet.Connection;
    var withTrans = conn.DbTransaction == null && updateSet.UseTransaction;
    try {
      LogComment(session, "-- SaveChanges starting, {0} records ------------", updateSet.Records.Count);
      var start = _timeService.ElapsedMilliseconds;
      if (withTrans)
        conn.BeginTransaction(commitOnSave: true);
      //execute commands
      await ExecuteScheduledCommandsAsync(conn, session, session.ScheduledCommandsAtStart);
      //Apply record updates  
      foreach (var grp in updateSet.UpdateGroups)
        foreach (var tableGrp in grp.TableGroups) {
          switch (tableGrp.Operation) {
            case LinqOperation.Insert:
              if (CanProcessMany(tableGrp)) {
                var refreshIdentyRefs = updateSet.InsertsIdentity && tableGrp.Table.Entity.Flags.IsSet(EntityFlags.ReferencesIdentity);
                if (refreshIdentyRefs)
                  RefreshIdentityReferences(updateSet, tableGrp.Records);
                var cmdBuilder = new DataCommandBuilder(this._driver, mode: SqlGenMode.PreferLiteral);
                var sql = SqlFactory.GetCrudInsertMany(tableGrp.Table, tableGrp.Records, cmdBuilder);
                cmdBuilder.AddInsertMany(sql, tableGrp.Records);
                var cmd = cmdBuilder.CreateCommand(conn, sql.ExecutionType, sql.ResultProcessor);
                await ExecuteDataCommandAsync(cmd);
              } else
                await SaveTableGroupRecordsOneByOneAsync(tableGrp, conn, updateSet);
              break;
            case LinqOperation.Update:
              await SaveTableGroupRecordsOneByOneAsync(tableGrp, conn, updateSet);
              break;
            case LinqOperation.Delete:
              if (CanProcessMany(tableGrp)) {
                var cmdBuilder = new DataCommandBuilder(this._driver);
                var sql = SqlFactory.GetCrudDeleteMany(tableGrp.Table);
                cmdBuilder.AddDeleteMany(sql, tableGrp.Records, new object[] { tableGrp.Records });
                var cmd = cmdBuilder.CreateCommand(conn, DbExecutionType.NonQuery, sql.ResultProcessor);
                await ExecuteDataCommandAsync(cmd);
              } else
                await SaveTableGroupRecordsOneByOneAsync(tableGrp, conn, updateSet);
              break;
          }
        } //foreach tableGrp
          //Execute scheduled commands
      await ExecuteScheduledCommandsAsync(conn, session, session.ScheduledCommandsAtEnd);
      if (conn.DbTransaction != null && conn.Flags.IsSet(DbConnectionFlags.CommitOnSave))
        conn.Commit();
      var end = _timeService.ElapsedMilliseconds;
      LogComment(session, "-- SaveChanges completed. Records: {0}, Time: {1} ms. ------------",
        updateSet.Records.Count, end - start);
      ReleaseConnection(conn);
    } catch {
      ReleaseConnection(conn, inError: true);
      throw;
    }
  }

  private async Task ExecuteScheduledCommandsAsync(DataConnection conn, EntitySession session, IList<LinqCommand> commands) {
    if (commands == null || commands.Count == 0)
      return;
    foreach (var cmd in commands)
      await ExecuteLinqNonQueryAsync(session, cmd, conn);
  }

  private async Task ExecuteBatchSingleCommandAsync(DbBatch batch) {
    var conn = batch.UpdateSet.Connection;
    try {
      var cmd = batch.Commands[0];
      await ExecuteBatchCommandAsync(cmd, conn);
      ReleaseConnection(conn);
    } catch {
      ReleaseConnection(conn, inError: true);
      throw;
    }
  }

  private async Task ExecuteBatchMultipleCommandsAsync(DbBatch batch) {
    //Note: for multiple commands, we cannot include trans statements into batch commands, like add 'Begin Trans' to the first command 
    //  and 'Commit' to the last command - this will fail. We start/commit trans using separate calls
    // Also, we have to manage connection explicitly, to start/commit transaction
    var conn = batch.UpdateSet.Connection;
    try {
      var inNewTrans = conn.DbTransaction == null;
      if (inNewTrans)
        conn.BeginTransaction(commitOnSave: true);
      foreach (var cmd in batch.Commands) {
        await ExecuteBatchCommandAsync(cmd, conn);
      }//foreach
      if (inNewTrans)
        conn.Commit();
      ReleaseConnection(conn);
    } catch {
      ReleaseConnection(conn, inError: true);
      throw;
    }
  }

  private async Task SaveTableGroupRecordsOneByOneAsync(DbUpdateTableGroup tableGrp, DataConnection conn, DbUpdateSet updateSet) {
    var checkIdentities = updateSet.InsertsIdentity && tableGrp.Table.Entity.Flags.IsSet(EntityFlags.ReferencesIdentity);
    foreach (var rec in tableGrp.Records) {
      if (checkIdentities)
        rec.RefreshIdentityReferences();
      var cmdBuilder = new DataCommandBuilder(this._driver);
      var sql = SqlFactory.GetCrudSqlForSingleRecord(tableGrp.Table, rec);
      cmdBuilder.AddRecordUpdate(sql, rec);
      var cmd = cmdBuilder.CreateCommand(conn, sql.ExecutionType, sql.ResultProcessor);
      await ExecuteDataCommandAsync(cmd);
    }
  }

  private async Task ExecuteBatchCommandAsync(DataCommand command, DataConnection conn) {
    if (command.ParamCopyList != null)
      foreach (var copy in command.ParamCopyList)
        copy.To.Value = copy.From.Value;
    await ExecuteDataCommandAsync(command);
  }


}
