using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Linq;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Driver;
using Vita.Data.Model;

namespace Vita.Data {
  public partial class Database {
    // Checks if batch is needed (record count > 1); and driver supports batch mode, 
    // and if there are any out params for CRUD stored procs (identity, timestamp) then driver supports out param for batch mode
    // MS SQL supports batch with output params; SQL CE does not support batch at all - one statement at a time.
    // MySql and Postgres support batch, but there are some troubles in using output parameters inside the batch,  
    // so for these drivers batch OutParamsInBatchMode is not set, so we use batch mode if there are none. 
    private bool ShouldUseBatchMode(UpdateSet updateSet) {
      var totalCount = updateSet.AllRecords.Count + updateSet.Session.ScheduledCommands.Count; 
      if (totalCount > 1 && Settings.ModelConfig.Options.IsSet(DbOptions.UseBatchMode)) {
        //Probably we should use batch mode; check if there any out params
        if (!updateSet.UsesOutParams || _driver.Supports(DbFeatures.OutParamsInBatchedUpdates))
          return true; 
      }
      return false;
    }

    // Note: scheduled commands are already in batch commands
    private void SaveChangesInBatchMode(UpdateSet updateSet) {
      var session = updateSet.Session;
      var postExecActions = new List<Action>();
      var batchBuilder = new DbBatchCommandSetBuilder(this, updateSet);
      batchBuilder.Build(); 
      LogComment(session, "-- BEGIN BATCH ({0} rows, {1} batch command(s)) ---------------------------", updateSet.AllRecords.Count, updateSet.BatchCommands.Count);
      if (updateSet.BatchCommands.Count == 1) {
        ExecuteBatchSingleCommand(updateSet);
      } else {
        ExecuteBatchMultipleCommands(updateSet);
      }
      LogComment(session, "-- END BATCH --------------------------------------\r\n");
      //execute post-execute actions; it is usually handling output parameter values
      // Finalize records after update
      foreach (var rec in updateSet.AllRecords) {
        rec.CustomTag = null; //clear temp ref that batch process has set
        rec.SubmitCount++;
        rec.EntityInfo.SaveEvents.OnSubmittedChanges(rec);
      }
    }
    private void ExecuteBatchSingleCommand(UpdateSet updateSet) {
      var conn = GetConnection(updateSet.Session);
      try {
        var batchCmd = updateSet.BatchCommands[0];
        var dbCmd = batchCmd.DbCommand;
        if (conn.DbTransaction == null) //surround it with beginTrans/commit
          dbCmd.CommandText = string.Format("{0}\r\n{1}\r\n{2}", _driver.BatchBeginTransaction, dbCmd.CommandText, _driver.BatchCommitTransaction);
        ExecuteDbCommand(dbCmd, conn, DbExecutionType.NonQuery);
        if (batchCmd.HasActions)
          foreach (var action in batchCmd.GetPostActions())
            action();
        ReleaseConnection(conn);
      } catch {
        ReleaseConnection(conn, inError: true);
        throw;
      }
    }

    private void ExecuteBatchMultipleCommands(UpdateSet updateSet) {
      //Note: for multiple commands, we cannot include trans statements into batch commands, like add 'Begin Trans' to the first command 
      //  and 'Commit' to the last command - this will fail. We start/commit trans using separate calls
      // Also, we have to manage connection explicitly, to start/commit transaction
      var conn = GetConnection(updateSet.Session);
      try {
        var inNewTrans = conn.DbTransaction == null; 
        if (inNewTrans)
          conn.BeginTransaction(commitOnSave: true);
        foreach (var batchCmd in updateSet.BatchCommands) {
          ExecuteDbCommand(batchCmd.DbCommand, conn, DbExecutionType.NonQuery);
          if (batchCmd.HasActions)
            foreach (var action in batchCmd.GetPostActions())
              action();
        }//foreach
        if (inNewTrans)
          conn.Commit(); 
        ReleaseConnection(conn);
      } catch {
        ReleaseConnection(conn, inError: true);
        throw;
      }
    }


  }//class
}
