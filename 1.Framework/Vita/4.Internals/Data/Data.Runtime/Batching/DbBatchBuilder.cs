using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Runtime;
using Vita.Data.Linq;
using Vita.Data.SqlGen;
using Vita.Data.Linq.Translation;

namespace Vita.Data.Runtime {

  internal class DbBatchBuilder {

    Database _db;
    DbDriver _driver;
    DbModel _dbModel;
    DataCommandRepo _commandRepo; 

    DbBatch _batch;

    DataCommandBuilder _commandBuilder;

    public DbBatchBuilder(Database db) {

      _db = db;
      _driver = _db.DbModel.Driver;
      _dbModel = _db.DbModel;
      _commandRepo = _db.CommandRepo;
    }

    public DbBatch Build(DbUpdateSet updateSet) {
      _batch = new DbBatch() { UpdateSet = updateSet };
      _commandBuilder = new DataCommandBuilder(_dbModel, batchMode: true);

      var commandsBefore = updateSet.Session.ScheduledCommands.Where(c => c.Schedule == CommandSchedule.TransactionStart).ToList();
      AddScheduledCommands(commandsBefore);
      foreach(var group in updateSet.UpdateGroups) 
        foreach(var tableGroup in group.TableGroups)  {
          AddUpdateGroup(tableGroup); 
        }// foreach group
      var commandsAfter = updateSet.Session.ScheduledCommands.Where(c => c.Schedule == CommandSchedule.TransactionEnd).ToList();
      AddScheduledCommands(commandsAfter);
      FinalizeCurrentCommand(completed: true);
      return _batch; 
    }

    protected void AddScheduledCommands(IList<EntityCommand> commands) {
      if(commands.Count == 0)
        return;
      foreach(var lcmd in commands) {
        CheckCurrentCommand();
        var sql = _commandRepo.GetLinqNonQuery(lcmd);
        _commandBuilder.AddLinqSql(sql, lcmd.ParameterValues); 
      }//foreach schCmd
    }

    protected void CheckCurrentCommand() {
      if(_commandBuilder.ParameterCount  >= _dbModel.Driver.SqlDialect.MaxParamCount) {
        FinalizeCurrentCommand();
        _commandBuilder = new DataCommandBuilder(_dbModel, batchMode: true);
      }
    }

    protected void FinalizeCurrentCommand(bool completed = false) {
      if(_commandBuilder == null)
        return;
      // if it is a single batch command with multiple SQL statements inside, then enclose it in begin/commit trans
      var alreadyInTrans = _batch.UpdateSet.Connection.DbTransaction != null;
      bool encloseInTrans = !alreadyInTrans && completed && _batch.Commands.Count == 0 && _commandBuilder.SqlCount > 1;
      var batchCmd = _commandBuilder.CreateBatchCommand(_batch.UpdateSet.Connection,  encloseInTrans); 
      _batch.Commands.Add(batchCmd);
    }

    public void AddUpdateGroup(DbUpdateTableGroup group) {
      SqlStatement  sql = null; 
      switch(group.Operation) {
        case EntityOperation.Insert:
          if(_db.CanProcessMany(group)) {
            // TODO: handle the case when there are too many records (too many params) within 1 insert command
            sql = _commandRepo.GetCrudInsertMany(group.Table, group.Records, _commandBuilder);
            _commandBuilder.AddSql(sql);
          } else {
            foreach(var rec in group.Records) {
              CheckCurrentCommand();
              sql = sql ?? _commandRepo.GetCrudNonQuery(group.Table, rec, _commandBuilder);
              _commandBuilder.AddSql(sql, rec);
            }
          }
          break;
        case EntityOperation.Update:
          foreach(var rec in group.Records) {
            CheckCurrentCommand();
            sql = _commandRepo.GetCrudNonQuery(group.Table, rec, _commandBuilder);
            _commandBuilder.AddSql(sql, rec);
          }
          break;
        case EntityOperation.Delete:
          if(_db.CanProcessMany(group)) {
            sql = _commandRepo.GetCrudDeleteMany(group.Table, group.Records);
            _commandBuilder.AddSql(sql);
          } else {
            foreach(var rec in group.Records) {
              CheckCurrentCommand();
              sql = sql ?? _commandRepo.GetCrudNonQuery(group.Table, rec, _commandBuilder);
              _commandBuilder.AddSql(sql, rec);
            }
          }
          break;

      }//switch
    }

  }//class
}//ns
