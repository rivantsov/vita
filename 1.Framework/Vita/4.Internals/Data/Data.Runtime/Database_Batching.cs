using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Driver;
using Vita.Data.Model;

namespace Vita.Data.Runtime {

  public partial class Database {
    // Checks if batch is needed (record count > 1); and driver supports batch mode, and if batch is not disabled; 
    // If there are any out params for CRUD stored procs (identity, timestamp) then driver should out param for batch mode
    // MS SQL supports batch with output params; SQL CE does not support batch at all - one statement at a time.
    // MySql and Postgres support batch, but there are some troubles in using output parameters inside the batch,  
    // so for these drivers batch OutParamsInBatchMode is not set, so we do not use batch mode if there are any. 
    private bool ShouldUseBatchMode(DbUpdateSet updateSet) {
      var canUseBatch = Settings.Driver.Supports(DbFeatures.BatchedUpdates) && Settings.ModelConfig.Options.IsSet(DbOptions.UseBatchMode);
      if(!canUseBatch)
        return false; 
      var totalCount = updateSet.Records.Count + updateSet.Session.ScheduledCommands.Count;
      if(totalCount <= 1)
        return false; 
      var options = updateSet.Session.Options;
      if (options.IsSet(EntitySessionOptions.DisableBatchMode))
        return false;
      // check if there any out params
      if(updateSet.InsertsIdentity && !_driver.Supports(DbFeatures.OutParamsInBatchedUpdates))
        return false; 
      return true;
    }

    // Note: scheduled commands are already in batch commands
    private void SaveChangesInBatchMode(DbUpdateSet updateSet) {
      var batchBuilder = new DbBatchBuilder(this);
      var batch = batchBuilder.Build(updateSet);
      LogComment(updateSet.Session, "-- BEGIN BATCH ({0} rows, {1} batch command(s)) ---------------------------",
              updateSet.Records.Count, batch.Commands.Count);
      if (batch.Commands.Count == 1) {
        ExecuteBatchSingleCommand(batch);
      } else {
        ExecuteBatchMultipleCommands(batch);
      }


      LogComment(updateSet.Session, "-- END BATCH --------------------------------------\r\n");

      var postExecActions = new List<Action>();

      var session = updateSet.Session;
      //execute post-execute actions; it is usually handling output parameter values
      // Finalize records after update
      foreach (var rec in updateSet.Records) {
        rec.SubmitCount++;
        rec.EntityInfo.SaveEvents.OnSubmittedChanges(rec);
      }
    }

    private void ExecuteBatchSingleCommand(DbBatch batch) {
      var conn = batch.UpdateSet.Connection;
      try {
        var cmd = batch.Commands[0];
        ExecuteBatchCommand(cmd, conn);
        ReleaseConnection(conn);
      } catch {
        ReleaseConnection(conn, inError: true);
        throw;
      }
    }

    private void ExecuteBatchMultipleCommands(DbBatch batch) {
      //Note: for multiple commands, we cannot include trans statements into batch commands, like add 'Begin Trans' to the first command 
      //  and 'Commit' to the last command - this will fail. We start/commit trans using separate calls
      // Also, we have to manage connection explicitly, to start/commit transaction
      var conn = batch.UpdateSet.Connection;
      try {
        var inNewTrans = conn.DbTransaction == null; 
        if (inNewTrans)
          conn.BeginTransaction(commitOnSave: true);
        foreach (var cmd in batch.Commands) {
          ExecuteBatchCommand(cmd, conn);
        }//foreach
        if (inNewTrans)
          conn.Commit(); 
        ReleaseConnection(conn);
      } catch {
        ReleaseConnection(conn, inError: true);
        throw;
      }
    }

    private void ExecuteBatchCommand(DataCommand command, DataConnection conn) {
      if(command.ParamCopyList != null)
        foreach(var copy in command.ParamCopyList)
          copy.To.Value = copy.From.Value;
      ExecuteDataCommand(command);
    }

  }//class
}
