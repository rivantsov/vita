using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Linq;
using Vita.Entities.Runtime;

namespace Vita.Data {
  internal class DbBatchCommandSetBuilder {
    public const int MaxLiteralLength = 100; 

    // Placed into EntityRecord.CustomTag to hold identity information
    class IdentitySource {
      public BatchDbCommand BatchCommand;
      public IDbDataParameter Parameter;
    }

    Database _db; 
    DbDriver _driver; 
    UpdateSet _updateSet;
    BatchDbCommand _currentCommand;

    StringBuilder _sqlBuilder;

    public DbBatchCommandSetBuilder(Database db, UpdateSet updateSet) {
      _db = db; 
      _driver = _db.DbModel.Driver; 
      _updateSet = updateSet;
      // Clear identity holders in record.CustomTag
      if (_updateSet.UsesOutParams)
        foreach (var rec in updateSet.AllRecords)
          rec.CustomTag = null; 
    }

    public void Build() {
      _updateSet.BatchCommands = new List<BatchDbCommand>();
      CheckCurrentCommand(); 
      var commandsBefore = _updateSet.Session.ScheduledCommands.Where(c => c.Schedule == CommandSchedule.TransactionStart).ToList();
      AddScheduledCommands(commandsBefore);
      foreach (var rec in _updateSet.AllRecords)
        AddRecordUpdateToBatch(rec);  
      var commandsAfter = _updateSet.Session.ScheduledCommands.Where(c => c.Schedule == CommandSchedule.TransactionEnd).ToList();
      AddScheduledCommands(commandsAfter); 
      FinalizeCurrentCommand(); 
    }

    private void AddRecordUpdateToBatch(EntityRecord rec) {
      CheckCurrentCommand();
      var dbCmd = _currentCommand.DbCommand; 
      var argValues = new List<string>();
      var cmdInfo = _db.GetDbCommandForSave(rec);
      foreach (var prmInfo in cmdInfo.Parameters) {
        var col = prmInfo.SourceColumn;
        //Check if we need to use already defined parameter; 
        // this happens when we insert parent and child records with parent having identity primary key column
        if (_updateSet.UsesOutParams && col != null && col.Flags.IsSet(DbColumnFlags.IdentityForeignKey)) {
          //Find out parameter that returns the identity of the parent record
          var parentRec = rec.GetValueDirect(col.Member.ForeignKeyOwner) as EntityRecord;
          if (parentRec != null && parentRec.CustomTag != null) {
            //parentRec has identity PK, and is already in _identities list
            var idSource = (IdentitySource) parentRec.CustomTag;
            if (idSource.BatchCommand == _currentCommand)
              argValues.Add(idSource.Parameter.ParameterName); //if it is the same command, just add ref to parameter
            else {
              //different command - create new parameter, and add action to copy param value from source
              // dbCmd.Parameters.Add(idSource.Parameter); //this does not work - parameters cannot be shared between commands
              var dbParam = _driver.AddParameter(dbCmd, prmInfo);
              //override parameter name
              dbParam.ParameterName = _driver.DynamicSqlParameterPrefix + "P" + (dbCmd.Parameters.Count - 1);
              argValues.Add(dbParam.ParameterName);
              var sourcePrm = idSource.Parameter;
              idSource.BatchCommand.AddPostAction(() => dbParam.Value = sourcePrm.Value);
            }
            continue; //next param
          }
        }//if 

        //Get the value, analyze it, see if it is ok to use literal or it's better to put the value into parameter
        var value = rec.GetValueDirect(col.Member);
        if (value == null)
          value = DBNull.Value;
        var conv = prmInfo.TypeInfo.PropertyToColumnConverter;
        if (value != DBNull.Value && conv != null)
          value = conv(value);
        if (BatchShouldUseParameterFor(prmInfo, value)) {
          //create parameter
          var dbParam = _driver.AddParameter(dbCmd, prmInfo);
          //override parameter name
          dbParam.ParameterName = _driver.DynamicSqlParameterPrefix + "P" + (dbCmd.Parameters.Count - 1);
          dbParam.Value = value;
          //If it is parameter holding identity returned from stored proc, then save its info in the rec.CustomTag
          bool isIdentityOut = rec.Status == EntityStatus.New && col.Flags.IsSet(DbColumnFlags.Identity) 
            && dbParam.Direction == ParameterDirection.Output;
          if (isIdentityOut) 
            rec.CustomTag = new IdentitySource() { BatchCommand = _currentCommand, Parameter = dbParam};

          //add reference to parameter in arg list
          var strArg = dbParam.ParameterName;
          if (dbParam.Direction != ParameterDirection.Input) {
            strArg = string.Format(_driver.CommandCallOutParamFormat, strArg);
            //copy value returned from sp into entity property
            _currentCommand.AddPostAction(() => 
                 rec.SetValueDirect(col.Member, dbParam.Value)
                 );
          }
          argValues.Add(strArg);
        } else {
          string argValue;
          if (value == DBNull.Value)
            argValue = "NULL";
          else
            argValue = prmInfo.TypeInfo.ToLiteral(value);
          argValues.Add(argValue);
        }// if BatchShouldUseParameterFor
      }//foreach prm
      //format method call
      var strArgs = string.Join(", ", argValues);
      var strCall = string.Format(_driver.CommandCallFormat, cmdInfo.FullCommandName, strArgs);
      //append it to sql
      _sqlBuilder.AppendLine(strCall);
    }

    private void AddScheduledCommands(IList<ScheduledLinqCommand> commands) {
      if (commands.Count == 0)
        return;
      var entModel = _db.DbModel.EntityApp.Model; 
      var engine = new Vita.Data.Linq.Translation.LinqEngine(_db.DbModel);
      object[] fmtArgs = new object[100];
      //specifics of formatting stored proc call - the first 2 args are always two braces
      // Braces in string literals are escaped and are represented as '{0}' and '{1}'  
      fmtArgs[0] = "{";
      fmtArgs[1] = "}";
      foreach (var schCmd in commands) {
        CheckCurrentCommand();
        var dbCmd = _currentCommand.DbCommand; 
        var linqCmd = schCmd.Command;
        LinqCommandAnalyzer.Analyze(entModel, linqCmd);
        var transCmd = engine.Translate(linqCmd);
        for (int prmIndex = 0; prmIndex < transCmd.Parameters.Count; prmIndex++) {
          var linqParam = transCmd.Parameters[prmIndex];
          var value = linqParam.ReadValue(linqCmd.ParameterValues) ?? DBNull.Value;
          var dbParam = dbCmd.CreateParameter();
          _db.DbModel.LinqSqlProvider.SetDbParameterValue(dbParam, value);
          var globalParamIndex = dbCmd.Parameters.Count;
          dbParam.ParameterName = _driver.DynamicSqlParameterPrefix + "P" + globalParamIndex;
          fmtArgs[prmIndex + 2] = dbParam.ParameterName;
          dbCmd.Parameters.Add(dbParam);
        }
        var sql = string.Format(transCmd.BatchSqlTemplate, fmtArgs);
        _sqlBuilder.AppendLine(sql);
      }//foreach schCmd
    }

    private bool BatchShouldUseParameterFor(DbParamInfo paramInfo, object value) {
      if (paramInfo.Direction != ParameterDirection.Input)
        return true;
      if (value == DBNull.Value)
        return false;
      var t = value.GetType();
      if (t == typeof(string)) {
        var str = (string)value;
        return str.Length > MaxLiteralLength;
      }
      if (t == typeof(byte[])) {
        var bytes = (byte[])value;
        return bytes.Length > MaxLiteralLength;
      }
      return false;
    }

    private void CheckCurrentCommand() {
      if (_currentCommand != null) {
        if (_currentCommand.DbCommand.Parameters.Count < _driver.MaxParamCount)
          return;
        FinalizeCurrentCommand();
      }
      //create new command
      _currentCommand = new BatchDbCommand() { DbCommand = _driver.CreateDbCommand() };
      _sqlBuilder = new StringBuilder(8192);
    }

    private void FinalizeCurrentCommand() {
      if (_currentCommand == null)
        return; 
      var sql = _sqlBuilder.ToString();
      if (string.IsNullOrWhiteSpace(sql))
        return; 
      _currentCommand.DbCommand.CommandText = sql;
      _updateSet.BatchCommands.Add(_currentCommand); 
      _currentCommand = null;
    }

  }//class
}//ns
