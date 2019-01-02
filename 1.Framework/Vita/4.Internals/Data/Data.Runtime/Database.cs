using System;
using System.Collections.Generic;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Entities.Services;
using Vita.Data.SqlGen;

namespace Vita.Data.Runtime {

  public partial class Database {
    public readonly DbModel DbModel;
    public readonly DbSettings Settings;
    public DataCommandRepo CommandRepo;

    /// <summary>Connection string.</summary>
    public string ConnectionString { get { return Settings.ConnectionString; } }
    private DbDriver _driver;
    private ITimeService _timeService;

    // constructors
    public Database(DbModel dbModel, DbSettings settings) {
      this.DbModel = dbModel;
      Settings = settings;
      _driver = Settings.Driver;
      _timeService = dbModel.EntityApp.TimeService;
      CommandRepo =  new DataCommandRepo(dbModel);
    }

    public void Shutdown() {
    }

    #region IDataStore Members

    public object ExecuteEntityCommand(EntitySession session, EntityCommand command) {
      var conn = GetConnectionWithLock(session, command.Info.LockType);
      try {
        object result;
        if(command.Operation == EntityOperation.Select)
          result = ExecuteEntitySelect(session, command, conn);
        else
          result = ExecuteEntityNonQuery(session, command, conn);
        ReleaseConnection(conn);
        return result;
      } catch(Exception dex) {
        ReleaseConnection(conn, inError: true);
        dex.AddValue(DataAccessException.KeyLinqQuery, command.QueryExpression);
        throw;
      }
    }

    public object ExecuteEntitySelect(EntitySession session, EntityCommand command, DataConnection conn) {
      var sql = CommandRepo.GetSelect(command); 
      var genMode = command.Info.Options.IsSet(QueryOptions.NoParameters) ? 
                             SqlGenMode.NoParameters : SqlGenMode.PreferParam;
      var cmdBuilder = new DataCommandBuilder(this._driver, batchMode: false, mode: genMode);
      cmdBuilder.AddLinqStatement(sql, command.ParameterValues);
      var dataCmd = cmdBuilder.CreateCommand(conn, DbExecutionType.Reader, sql.ResultProcessor);
      ExecuteDataCommand(dataCmd);
      return dataCmd.ProcessedResult;
    }

    private object ExecuteEntityNonQuery(EntitySession session, EntityCommand command, DataConnection conn) {
      var sql = CommandRepo.GetLinqNonQuery(command);
      var fmtOptions = command.Info.Options.IsSet(QueryOptions.NoParameters) ?
                             SqlGenMode.NoParameters : SqlGenMode.PreferParam;
      var cmdBuilder = new DataCommandBuilder(this._driver);
      cmdBuilder.AddLinqStatement(sql, command.ParameterValues);
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
      session.ScheduledCommands.Clear(); 
    }

    private void SaveChangesNoBatch(DbUpdateSet updateSet) {
      var session = updateSet.Session; 
      var conn = updateSet.Connection;
      var withTrans = conn.DbTransaction == null && updateSet.UseTransaction; 
      try {
        var start = GetCurrentMsCount();
        if(withTrans)
          conn.BeginTransaction(commitOnSave: true);
        //execute commands
        if (session.ScheduledCommands.Count > 0)
          ExecuteScheduledCommands(conn, session, CommandSchedule.TransactionStart);
        //Apply record updates  
        foreach (var grp in updateSet.UpdateGroups)
          foreach (var tableGrp in grp.TableGroups) {
            switch(tableGrp.Operation) {
              case EntityOperation.Insert:
                if (CanProcessMany(tableGrp)) {
                  var cmdBuilder = new DataCommandBuilder(this._driver);
                  var sql = CommandRepo.GetCrudInsertMany(tableGrp.Table, tableGrp.Records, cmdBuilder);
                  cmdBuilder.AddUpdates(sql, tableGrp.Records);
                  var cmd = cmdBuilder.CreateCommand(conn, sql.ExecutionType, sql.ResultProcessor);
                  ExecuteDataCommand(cmd);
                } else
                  SaveTableGroupRecordsOneByOne(tableGrp, conn, updateSet);
                break;
              case EntityOperation.Update:
                SaveTableGroupRecordsOneByOne(tableGrp, conn, updateSet);
                break;
              case EntityOperation.Delete:
                if (CanProcessMany(tableGrp)) {
                  var cmdBuilder = new DataCommandBuilder(this._driver);
                  var sql = CommandRepo.GetCrudDeleteMany(tableGrp.Table);
                  cmdBuilder.AddUpdates(sql, tableGrp.Records, new object[] { tableGrp.Records });
                  var cmd = cmdBuilder.CreateCommand(conn, DbExecutionType.NonQuery, sql.ResultProcessor);
                  ExecuteDataCommand(cmd);
                } else
                  SaveTableGroupRecordsOneByOne(tableGrp, conn, updateSet);
                break; 
            }
          } //foreach tableGrp
        //Execute scheduled commands
        if (session.ScheduledCommands.Count > 0)
          ExecuteScheduledCommands(conn, session, CommandSchedule.TransactionEnd);
        if (conn.DbTransaction != null && conn.Flags.IsSet(DbConnectionFlags.CommitOnSave))
          conn.Commit(); 
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
        var sql = CommandRepo.GetCrudSqlForSingleRecord(tableGrp.Table, rec);
        cmdBuilder.AddUpdate(sql, rec);
        var cmd = cmdBuilder.CreateCommand(conn, sql.ExecutionType, sql.ResultProcessor);
        ExecuteDataCommand(cmd);
      }
    }


    private void ExecuteScheduledCommands(DataConnection conn, EntitySession session, CommandSchedule schedule) {
      if (session.ScheduledCommands.Count == 0)
        return;
      foreach (var cmd in session.ScheduledCommands)
        if (cmd.Schedule == schedule) {
          ExecuteEntityNonQuery(session, cmd, conn);
        }
    }


    #endregion 

    public bool CanProcessMany(DbUpdateTableGroup group) {
      if (group.Records.Count <= 1)
        return false;
      switch (group.Operation) {
        case EntityOperation.Delete:
          return group.Table.PrimaryKey.KeyColumns.Count == 1;
        case EntityOperation.Insert:
          if(!_driver.Supports(DbFeatures.InsertMany))
            return false;
          if(group.Table.Entity.Flags.IsSet(EntityFlags.HasIdentity | EntityFlags.HasRowVersion))
            return false;
          return true;
        default:
          return false;
      }
    }


    private long GetCurrentMsCount() {
      return _timeService.ElapsedMilliseconds;
    }

  }//class

}//ns
