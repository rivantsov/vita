using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Utilities;
using Vita.Data.Linq;
using System.Linq;
using Vita.Data;

namespace Vita.Entities.Runtime {

  public partial class EntitySession {

    public TResult ExecuteQuery<TResult>(Expression expression) {
      var objResult = ExecuteQuery(expression);
      if(objResult == null)
        return default(TResult);
      var objType = objResult.GetType();
      if(typeof(TResult).IsAssignableFrom(objType))
        return (TResult)objResult;
      //one special case - when TResult is IEnumerable<T> but query returns IEnumerable<T?>
      var resType = typeof(TResult);
      if(resType.IsGenericType && resType.GetGenericTypeDefinition() == typeof(IEnumerable<>)) {
        var list = ConvertHelper.ConvertEnumerable(objResult as IEnumerable, resType);
        return (TResult)list;
      }
      Util.Throw($"Failed to convert query result of type {objType} to type {resType}.\r\n Query: {expression}");
      return default(TResult);
    }

    public object ExecuteQuery(Expression expression) {
      var command = LinqCommandFactory.CreateLinqSelect(this, expression);
      var result = ExecuteLinqCommand(command);
      return result;
    }

    public virtual object ExecuteLinqCommand(LinqCommand command, bool withIncludes = true) {
      try {
        var result = _dataSource.ExecuteLinqCommand(this, command);
        switch(command.Operation) {
          case LinqOperation.Select:
            Context.App.AppEvents.OnExecutedSelect(this, command);
            if(withIncludes && (command.Includes?.Count > 0 || Context.HasIncludes()))
              this.RunIncludeQueries(command, result);
            break;
          default:
            Context.App.AppEvents.OnExecutedNonQuery(this, command);
            break;
        }
        return result;
      } catch(Exception ex) {
        ex.AddValue("entity-command", command + string.Empty);
        ex.AddValue("parameters", command.ParamValues);
        throw;
      }
    }

    internal int ExecuteLinqNonQuery<TEntity>(IQueryable baseQuery, LinqOperation operation) {
      Util.CheckParam(baseQuery, nameof(baseQuery));
      var updateEnt = Context.App.Model.GetEntityInfo(typeof(TEntity));
      var command = LinqCommandFactory.CreateLinqNonQuery(this, baseQuery.Expression, operation, updateEnt);
      var objResult = this.ExecuteLinqCommand(command);
      return (int)objResult;
    }

    public IEntityRecordContainer SelectByPrimaryKey(EntityInfo entity, object[] keyValues,
            LockType lockType = LockType.None, EntityMemberMask mask = null) {
      var cmd = LinqCommandFactory.CreateSelectByPrimaryKey(this, entity.PrimaryKey, lockType, keyValues);
      var list = (IList)ExecuteLinqCommand(cmd);
      if(list.Count == 0)
        return null;
      return (IEntityRecordContainer)list[0];
    }

    public void ScheduleLinqNonQuery<TEntity>(IQueryable baseQuery, LinqOperation op,
                             CommandSchedule schedule = CommandSchedule.TransactionEnd) {
      Util.Check(baseQuery is EntityQuery, "query parameter should an EntityQuery.");
      var model = Context.App.Model;
      var targetEnt = model.GetEntityInfo(typeof(TEntity));
      Util.Check(targetEnt != null, "Generic parameter {0} is not an entity registered in the Model.", typeof(TEntity));
      var command = LinqCommandFactory.CreateLinqNonQuery(this, baseQuery.Expression, op, targetEnt);
      switch(schedule) {
        case CommandSchedule.TransactionStart:
          ScheduledCommandsAtStart = ScheduledCommandsAtStart ?? new List<LinqCommand>();
          ScheduledCommandsAtStart.Add(command);
          break;
        case CommandSchedule.TransactionEnd:
          ScheduledCommandsAtEnd = ScheduledCommandsAtEnd ?? new List<LinqCommand>();
          ScheduledCommandsAtEnd.Add(command);
          break;
      }
    }

    public int ScheduledCommandsCount() {
      int cnt = 0;
      if(ScheduledCommandsAtStart != null)
        cnt += ScheduledCommandsAtStart.Count;
      if(ScheduledCommandsAtEnd != null)
        cnt += ScheduledCommandsAtEnd.Count;
      return cnt;
    }

    private void RunIncludeQueries(LinqCommand command, object mainQueryResult) {
      // initial checks if there's anything to run
      if (mainQueryResult == null)
        return;
      var allIncludes = this.Context.GetMergedIncludes(command.Includes);
      if (allIncludes == null || allIncludes.Count == 0)
        return;
      var resultShape = MemberLoadHelper.GetResultShape(Context.App.Model, mainQueryResult.GetType());
      if (resultShape == QueryResultShape.Object)
        return;
      // Get records from query result
      var records = new List<EntityRecord>();
      switch (resultShape) {
        case QueryResultShape.Entity:
          records.Add(EntityHelper.GetRecord(mainQueryResult));
          break;
        case QueryResultShape.EntityList:
          var list = mainQueryResult as IList;
          if (list.Count == 0)
            return;
          foreach (var ent in list)
            records.Add(EntityHelper.GetRecord(ent));
          break;
      }//switch;
      // actually run the includes
      var entityType = records[0].EntityInfo.EntityType;
      var helper = new IncludeProcessor(this, allIncludes);
      this.LogMessage("------- Running include queries   ----------");
      helper.RunIncludeQueries(entityType, records);
      this.LogMessage("------- Completed include queries ----------");
    }



  } //class
}
