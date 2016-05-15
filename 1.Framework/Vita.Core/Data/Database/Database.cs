using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Model;

using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Entities.Services;
using Vita.Entities.Linq;

namespace Vita.Data {

  public partial class Database : IDataStore {
    private EntityApp _app; 
    public DbModel DbModel { get; private set; }
    public readonly DbSettings Settings;

    /// <summary>Connection string.</summary>
    public string ConnectionString { get { return Settings.ConnectionString; } }
    private DbDriver _driver;
    private ITimeService _timeService;
    private EntityModel _entityModel;
    object _lock = new object();

    // constructors
    public Database(EntityApp app, DbSettings settings) {
      _app = app; 
      Settings = settings;
      _driver = Settings.ModelConfig.Driver;
      _entityModel = app.Model;
      _timeService = _app.TimeService;
      //Set list of all schemas
      var allSchemas = app.Areas.Select(a => settings.ModelConfig.GetSchema(a));
      settings.SetSchemas(allSchemas);

      //Check if model is shared
      bool modelIsShared = Settings.ModelConfig.Options.IsSet(DbOptions.ShareDbModel);
      lock (_lock) { //we need lock to prevent collision on shared model
          if (modelIsShared)
            DbModel = Settings.ModelConfig.SharedDbModel;
          if (DbModel == null) {
            var dbmBuilder = new DbModelBuilder(app.Model, settings.ModelConfig, app.ActivationLog);
            DbModel = dbmBuilder.Build();
            if (modelIsShared)
              Settings.ModelConfig.SharedDbModel = DbModel;
          }
        }//lock
      
      //Save 
    }

    public void Shutdown() {
    }

    public bool CheckConnectivity() {
      IDbConnection conn = null;
      try {
        conn = this.DbModel.Driver.CreateConnection(this.ConnectionString);
        conn.Open();
        return true;
      } catch(Exception) {
        return false;
      } finally {
        if(conn != null)
          conn.Close();
      }
    }


    #region IDataStore Members

    public IList<EntityRecord> ExecuteSelect(EntityCommand command, EntitySession session, object[] args) {
      EntityInfo entInfo = command.TargetEntityInfo;
      var dbCommandInfo = GetDbCommandInfo(command);
      if (dbCommandInfo == null) {
        //check if it is request for SelectAllPaged and driver does not support paging - then substitute with SelecAll
        if (command == entInfo.CrudCommands.SelectAllPaged)
          dbCommandInfo = GetDbCommandInfo(entInfo.CrudCommands.SelectAll);
      }
      if (dbCommandInfo == null)
        Util.Throw("Db command not found for entity command " + command.CommandName);
      var conn = GetConnection(session);
      try {
        var cmd = CreateDbCommand(dbCommandInfo, conn);
        if (dbCommandInfo.IsTemplatedSql)
          FormatTemplatedSql(dbCommandInfo, cmd, args);
        else 
          SetCommandParameterValues(dbCommandInfo, cmd, args);
        var records = new List<EntityRecord>();
        var result = ExecuteDbCommand(cmd, conn, DbExecutionType.Reader,
          (reader) => {
            while(reader.Read())
              records.Add(dbCommandInfo.EntityMaterializer.ReadRecord(reader, session));
            return records.Count;
            });
        ReleaseConnection(conn);
        return records;
      } catch {
        ReleaseConnection(conn, true);
        throw; 
      }
    }

    public void SaveChanges(EntitySession session) {
      if (session.HasChanges()) {
        //check if we need to sequence records
        var useRefIntegrity = Settings.ModelConfig.Options.IsSet(DbOptions.UseRefIntegrity);
        var canDeferIntegrCheck = _driver.Supports(DbFeatures.DeferredConstraintCheck);
        var needSequencing = useRefIntegrity && !canDeferIntegrCheck;
        var records = session.RecordsChanged.Where(rec => ShouldUpdate(rec)).ToList();
        var updateSet = new UpdateSet(session, _timeService.UtcNow, records, needSequencing);
        var batchMode = ShouldUseBatchMode(updateSet);
        if (batchMode)
          SaveChangesInBatchMode(updateSet);
        else
          SaveChangesNoBatch(session, updateSet);
      }
      //commit if we have session connection with transaction and CommitOnSave
      var conn = session.CurrentConnection;
      if (conn != null) {
        if (conn.DbTransaction != null && conn.Flags.IsSet(ConnectionFlags.CommitOnSave))
          conn.Commit();
        if (conn.Lifetime != ConnectionLifetime.Explicit)
          ReleaseConnection(conn); 
      }
      session.ScheduledCommands.Clear(); 
    }

    private void SaveChangesNoBatch(EntitySession session, UpdateSet updateSet) {
      var conn = GetConnection(session);
      var withTrans = conn.DbTransaction == null && updateSet.UseTransaction; 
      try {
        var start = CurrentTickCount;
        if(withTrans)
          conn.BeginTransaction(commitOnSave: true);
        //execute commands
        if (session.ScheduledCommands.Count > 0)
          ExecuteScheduledCommands(conn, session, CommandSchedule.TransactionStart); 
        //Apply record updates  
        foreach(var rec in updateSet.AllRecords)
          ApplyUpdate(conn, rec);
        //Execute scheduled commands
        if (session.ScheduledCommands.Count > 0)
          ExecuteScheduledCommands(conn, session, CommandSchedule.TransactionEnd);
        if (conn.DbTransaction != null && conn.Flags.IsSet(ConnectionFlags.CommitOnSave))
          conn.Commit(); 
        ReleaseConnection(conn);
      } catch {
        ReleaseConnection(conn, inError: true);
        throw; 
      }
    }

    private void ExecuteScheduledCommands(DataConnection conn, EntitySession session, CommandSchedule schedule) {
      if (session.ScheduledCommands.Count == 0)
        return;
      foreach (var cmd in session.ScheduledCommands)
        if (cmd.Schedule == schedule) {
          ExecuteLinqNonQuery(cmd.Command, session, conn);
        }
    }

    private bool ShouldUpdate(EntityRecord record) {
      if (record.Status == EntityStatus.Modified && record.EntityInfo.Flags.IsSet(EntityFlags.NoUpdate))
        return false; //if for whatever reason we have such a record, just ignore it
      if(record.Status == EntityStatus.Fantom)
        return false; 
      return true;
    }


    #endregion 

    internal DbCommandInfo GetDbCommandForSave(EntityRecord record) {
      var crud = record.EntityInfo.CrudCommands;
      EntityCommand entCmd;
      switch (record.Status) {
        case EntityStatus.New: entCmd = crud.Insert; break;
        case EntityStatus.Modified: entCmd = crud.Update; break;
        case EntityStatus.Deleting: entCmd = crud.Delete; break;
        default:
          return null;
      }
      var cmdInfo = GetDbCommandInfo(entCmd);
      return cmdInfo;
    }

    private void ApplyUpdate(DataConnection connection, EntityRecord record) {
      var cmdInfo = GetDbCommandForSave(record);
      Util.Check(cmdInfo != null, "Failed to find update/insert/delete command for entity {0}, status {1).", 
                       record.EntityInfo.Name, record.Status);
      try {
        var cmd = CreateDbCommand(cmdInfo, connection);
        SetCrudCommandParameterValues(cmdInfo, cmd, record);
        ExecuteDbCommand(cmd, connection, DbExecutionType.NonQuery);
        if(cmdInfo.PostUpdateActions.Count > 0)
          foreach(var action in cmdInfo.PostUpdateActions)
            action(connection, cmd, record);
        record.SubmitCount++;
        record.EntityInfo.SaveEvents.OnSubmittedChanges(record);
      } catch(Exception ex) {
        ex.AddValue("entity-command-name", cmdInfo.EntityCommand.CommandName);
        ex.AddValue("record", record);
        throw;
      }
    }

    private IDbCommand CreateDbCommand(DbCommandInfo commandInfo, DataConnection connection) {
      var cmd = _driver.CreateDbCommand(commandInfo);
      cmd.Connection = connection.DbConnection;
      cmd.Transaction = connection.DbTransaction;
      connection.Session.SetLastCommand(cmd);
      return cmd;
    }

    // Sets parameter values.
    private void SetCommandParameterValues(DbCommandInfo commandInfo, IDbCommand command, object[] args) {
      for (int i = 0; i < commandInfo.Parameters.Count; i++) {
        var prmInfo = commandInfo.Parameters[i];
        if (prmInfo.ArgIndex >= 0) {
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
      if (args == null || args.Length == 0)
        return; 
      var values = new string[args.Length];
      for (int i = 0; i < commandInfo.Parameters.Count; i++) {
        var prmInfo = commandInfo.Parameters[i];
        if (prmInfo.ArgIndex >= 0) {
          var v = args[prmInfo.ArgIndex];
          var strV = prmInfo.ToLiteralConverter(v);
          values[i] = strV;
        }
        command.CommandText = string.Format(commandInfo.Sql, values);
      } //for i
    }


    #region private utilities

    private DbCommandInfo GetDbCommandInfo(EntityCommand entityCommand) {
      var cmd = DbModel.LookupDbObject<DbCommandInfo>(entityCommand);
      if (cmd == null) {
        Util.Throw("Command {0} not implemented by the database. Most likely driver does not support this type of command.", entityCommand.CommandName);
      }        
      return cmd;
    }

    private void SetCrudCommandParameterValues(DbCommandInfo commandInfo, IDbCommand command, EntityRecord record) {
      if (record.Status == EntityStatus.Stub)
        record.Reload();
      for (int i = 0; i < commandInfo.Parameters.Count; i++) {
        var prm = (IDbDataParameter)command.Parameters[i];
        prm.Value = DBNull.Value;
        var prmInfo = commandInfo.Parameters[i];
        var isInput = prmInfo.Direction == ParameterDirection.Input || prmInfo.Direction == ParameterDirection.InputOutput;
        if (!isInput)  continue; 
        var col = prmInfo.SourceColumn;
        if (col == null || col.Member == null) continue; 
        var value = record.GetValueDirect(col.Member);
        if(value == null)
          value = DBNull.Value;
        var conv = prmInfo.TypeInfo.PropertyToColumnConverter;
        if (value != DBNull.Value && conv != null)
          value = conv(value);
        prm.Value = value; 
      } //for i
    }

    protected void ReadCrudOutputParameterValues(IDbCommand command, DbCommandInfo commandInfo, EntityRecord record) {
      for (int i = 0; i < commandInfo.OutputParameters.Count; i++) {
        var prmInfo = commandInfo.OutputParameters[i];
        var col = prmInfo.SourceColumn;
        if (col == null) continue; 
        var prm = command.Parameters[prmInfo.Name] as IDbDataParameter;
        var value = prm.Value; 
        var conv = prmInfo.TypeInfo.ColumnToPropertyConverter;
        if (value != DBNull.Value && conv != null)
          value = conv(value);
        record.ValuesModified[col.Member.ValueIndex] = value; 
      }//for
    }

    #endregion

    private long CurrentTickCount {
      get {
        return _timeService == null ? -1 : _timeService.ElapsedMilliseconds;
      }
    }

  }//class

}//ns
