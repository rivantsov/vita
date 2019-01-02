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
      _commandBuilder = new DataCommandBuilder(_driver, batchMode: true, mode: SqlGenMode.PreferLiteral);

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
        _commandBuilder.AddLinqStatement(sql, lcmd.ParameterValues); 
      }//foreach schCmd
    }

    protected void CheckCurrentCommand() {
      if(_commandBuilder.ParameterCount  >= _driver.SqlDialect.MaxParamCount) {
        FinalizeCurrentCommand();
        _commandBuilder = new DataCommandBuilder(_driver, batchMode: true);
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
            // TODO: handle the case when there are too many params within 1 insert command
            var recGroups = GroupRecordsForInsertMany(group.Records, _driver.SqlDialect.MaxRecordsInInsertMany);
            foreach(var recGroup in recGroups) {
              sql = _commandRepo.GetCrudInsertMany(group.Table, recGroup, _commandBuilder);
              _commandBuilder.AddUpdates(sql, recGroup);
            } //foreach
          } else {
            foreach(var rec in group.Records) {
              CheckCurrentCommand();
              sql = sql ?? _commandRepo.GetCrudSqlForSingleRecord(group.Table, rec);
              _commandBuilder.AddUpdate(sql, rec);
            }
          }
          break;

        case EntityOperation.Update:
          foreach(var rec in group.Records) {
            CheckCurrentCommand();
            sql = _commandRepo.GetCrudSqlForSingleRecord(group.Table, rec);
            _commandBuilder.AddUpdate(sql, rec);
          }
          break;

        case EntityOperation.Delete:
          if(_db.CanProcessMany(group)) {
            sql = _commandRepo.GetCrudDeleteMany(group.Table);
            _commandBuilder.AddUpdates(sql, group.Records, new object[] { group.Records });  
          } else {
            foreach(var rec in group.Records) {
              CheckCurrentCommand();
              sql = sql ?? _commandRepo.GetCrudSqlForSingleRecord(group.Table, rec);
              _commandBuilder.AddUpdate(sql, rec);
            }
          }
          break;
      }//switch
    }

    // # of records in one insert is limited (1000 for MS SQL), so we have to group them 
    private IList<IList<EntityRecord>> GroupRecordsForInsertMany(IList<EntityRecord> records, int maxInGroup) {
      // shortcut
      if (records.Count < maxInGroup)
        return new[] { records };
      var groups = new List<IList<EntityRecord>>();
      IList<EntityRecord> currGroup = null;
      foreach (var rec in records) {
        if (currGroup == null || currGroup.Count >= maxInGroup) {
          currGroup = new List<EntityRecord>();
          groups.Add(currGroup);
        }
        currGroup.Add(rec);
      }// foreach rec
      return groups;
    }

  }//class
}//ns
