using System;
using System.Collections.Generic;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Entities.Services;
using Vita.Data.Sql;

namespace Vita.Data.Runtime {

  public partial class Database {
    public readonly DbModel DbModel;
    public readonly DbSettings Settings;
    public readonly SqlFactory SqlFactory;

    private DbDriver _driver;
    private ITimeService _timeService;

    // constructors
    public Database(DbModel dbModel, DbSettings settings) {
      this.DbModel = dbModel;
      Settings = settings;
      _driver = Settings.Driver;
      _timeService = dbModel.EntityApp.TimeService;
      SqlFactory =  new SqlFactory(dbModel);
    }

    public void Shutdown() {
    }

    public object ExecuteLinqCommand(EntitySession session, ExecutableLinqCommand command) {
      var conn = GetConnectionWithLock(session, command.BaseCommand.LockType);
      try {
        object result;
        if(command.BaseCommand.Operation == LinqOperation.Select)
          result = ExecuteLinqSelect(session, command, conn);
        else
          result = ExecuteLinqNonQuery(session, command, conn);
        ReleaseConnection(conn);
        return result;
      } catch(Exception dex) {
        ReleaseConnection(conn, inError: true);
        dex.AddValue(DataAccessException.KeyLinqQuery, command.ToString());
        throw;
      }
    }

    public object ExecuteLinqSelect(EntitySession session, ExecutableLinqCommand command, DataConnection conn) {
      var sql = SqlFactory.GetLinqSql(command.BaseCommand); 
      var genMode = command.BaseCommand.Options.IsSet(QueryOptions.NoParameters) ? 
                             SqlGenMode.NoParameters : SqlGenMode.PreferParam;
      var cmdBuilder = new DataCommandBuilder(this._driver, batchMode: false, mode: genMode);
      cmdBuilder.AddLinqStatement(sql, command.ParamValues);
      var dataCmd = cmdBuilder.CreateCommand(conn, DbExecutionType.Reader, sql.ResultProcessor);
      ExecuteDataCommand(dataCmd);
      return dataCmd.ProcessedResult;
    }

    private object ExecuteLinqNonQuery(EntitySession session, ExecutableLinqCommand command, DataConnection conn) {
      var sql = SqlFactory.GetLinqSql(command.BaseCommand);
      var fmtOptions = command.BaseCommand.Options.IsSet(QueryOptions.NoParameters) ?
                             SqlGenMode.NoParameters : SqlGenMode.PreferParam;
      var cmdBuilder = new DataCommandBuilder(this._driver);
      cmdBuilder.AddLinqStatement(sql, command.ParamValues);
      var dataCmd = cmdBuilder.CreateCommand(conn, DbExecutionType.NonQuery, sql.ResultProcessor);
      ExecuteDataCommand(dataCmd);
      return dataCmd.ProcessedResult ?? dataCmd.Result;
    }

    public void SaveChanges(EntitySession session) {
      if (session.HasChanges()) {
        var conn = GetConnection(session);
        var updateSet = new DbUpdateSet(session, this.DbModel, conn);
        var batchMode = ShouldUseBatchMode(updateSet);
        if (batchMode)
          SaveChangesInBatchMode(updateSet);
        else
          SaveChangesNoBatch(updateSet);
      }
      //commit if we have session connection with transaction and CommitOnSave
      var sConn = session.CurrentConnection;
      if (sConn != null) {
        if (sConn.DbTransaction != null && sConn.Flags.IsSet(DbConnectionFlags.CommitOnSave))
          sConn.Commit();
        if (sConn.Lifetime != DbConnectionLifetime.Explicit)
          ReleaseConnection(sConn); 
      }
      session.ScheduledCommandsAtStart.Clear();
      session.ScheduledCommandsAtEnd.Clear();
    }

    private void SaveChangesNoBatch(DbUpdateSet updateSet) {
      var session = updateSet.Session; 
      var conn = updateSet.Connection;
      var withTrans = conn.DbTransaction == null && updateSet.UseTransaction; 
      try {
        LogComment(session, "-- SaveChanges starting, {0} records ------------", updateSet.Records.Count);
        var start = _timeService.ElapsedMilliseconds;
        if(withTrans)
          conn.BeginTransaction(commitOnSave: true);
        //execute commands
        ExecuteScheduledCommands(conn, session, session.ScheduledCommandsAtStart);
        //Apply record updates  
        foreach (var grp in updateSet.UpdateGroups)
          foreach (var tableGrp in grp.TableGroups) {
            switch(tableGrp.Operation) {
              case LinqOperation.Insert:
                if (CanProcessMany(tableGrp)) {
                  var cmdBuilder = new DataCommandBuilder(this._driver, mode: SqlGenMode.PreferLiteral);
                  var sql = SqlFactory.GetCrudInsertMany(tableGrp.Table, tableGrp.Records, cmdBuilder);
                  cmdBuilder.AddInsertMany(sql, tableGrp.Records);
                  var cmd = cmdBuilder.CreateCommand(conn, sql.ExecutionType, sql.ResultProcessor);
                  ExecuteDataCommand(cmd);
                } else
                  SaveTableGroupRecordsOneByOne(tableGrp, conn, updateSet);
                break;
              case LinqOperation.Update:
                SaveTableGroupRecordsOneByOne(tableGrp, conn, updateSet);
                break;
              case LinqOperation.Delete:
                if (CanProcessMany(tableGrp)) {
                  var cmdBuilder = new DataCommandBuilder(this._driver);
                  var sql = SqlFactory.GetCrudDeleteMany(tableGrp.Table);
                  cmdBuilder.AddDeleteMany(sql, tableGrp.Records, new object[] { tableGrp.Records });
                  var cmd = cmdBuilder.CreateCommand(conn, DbExecutionType.NonQuery, sql.ResultProcessor);
                  ExecuteDataCommand(cmd);
                } else
                  SaveTableGroupRecordsOneByOne(tableGrp, conn, updateSet);
                break; 
            }
          } //foreach tableGrp
        //Execute scheduled commands
        ExecuteScheduledCommands(conn, session, session.ScheduledCommandsAtEnd);
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

    private void SaveTableGroupRecordsOneByOne(DbUpdateTableGroup tableGrp, DataConnection conn, DbUpdateSet updateSet) {
      foreach (var rec in tableGrp.Records) {
        if(updateSet.InsertsIdentity && rec.EntityInfo.Flags.IsSet(EntityFlags.ReferencesIdentity))
          rec.RefreshIdentityReferences(); 
        var cmdBuilder = new DataCommandBuilder(this._driver);
        var sql = SqlFactory.GetCrudSqlForSingleRecord(tableGrp.Table, rec);
        cmdBuilder.AddRecordUpdate(sql, rec);
        var cmd = cmdBuilder.CreateCommand(conn, sql.ExecutionType, sql.ResultProcessor);
        ExecuteDataCommand(cmd);
      }
    }

    private void ExecuteScheduledCommands(DataConnection conn, EntitySession session, IList<LinqCommand> commands) {
      foreach (var cmd in commands)
          ExecuteLinqNonQuery(session, new ExecutableLinqCommand(cmd), conn);
    }

    public bool CanProcessMany(DbUpdateTableGroup group) {
      if (group.Records.Count <= 1)
        return false;
      switch (group.Operation) {
        case LinqOperation.Delete:
          return group.Table.PrimaryKey.KeyColumns.Count == 1;
        case LinqOperation.Insert:
          if(!_driver.Supports(DbFeatures.InsertMany))
            return false;
          if(group.Table.Entity.Flags.IsSet(EntityFlags.HasIdentity | EntityFlags.HasRowVersion))
            return false;
          return true;
        default:
          return false;
      }
    }


  }//class

}//ns
