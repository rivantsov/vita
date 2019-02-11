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
      var command = new DynamicLinqCommand(this, LinqCommandSource.Dynamic, LinqOperation.Select, expression);
      var execCommand = new ExecutableLinqCommand(command);
      var result = ExecuteLinqCommand(execCommand);
      return result;
    }


    public virtual object ExecuteLinqCommand(ExecutableLinqCommand command, bool withIncludes = true) {
      try {
        var result = _dataSource.ExecuteLinqCommand(this, command);
        switch(command.BaseCommand.Operation) {
          case LinqOperation.Select:
            Context.App.AppEvents.OnExecutedSelect(this, command);
            if(withIncludes && (command.BaseCommand.Includes?.Count > 0 || Context.HasIncludes()))
              IncludeProcessor.RunIncludeQueries(this, command, result);
            break;
          default:
            Context.App.AppEvents.OnExecutedNonQuery(this, command);
            NextTransactionId = Guid.NewGuid();
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
      var command = new DynamicLinqCommand(this, LinqCommandSource.Dynamic, operation, baseQuery.Expression, updateEnt);
      var execCommand = new ExecutableLinqCommand(command); 
      var objResult = this.ExecuteLinqCommand(execCommand);
      return (int)objResult;
    }

    public IEntityRecordContainer SelectByPrimaryKey(EntityInfo entity, object[] keyValues,
            LockType lockType = LockType.None, EntityMemberMask mask = null) {
      mask = mask ?? entity.AllMembersMask;
      LinqCommand cmd = GetSelectByKeyCommand(entity.PrimaryKey, lockType, mask);
      var execCmd = new ExecutableLinqCommand(cmd, keyValues);
      var list = (IList)ExecuteLinqCommand(execCmd);
      if(list.Count == 0)
        return null;
      return (IEntityRecordContainer)list[0];
    }

    public LinqCommand GetSelectByKeyCommand(EntityKeyInfo key, LockType lockType, EntityMemberMask mask) {
      if(mask == null && lockType == LockType.None) {
        key.SelectByKeyCommand = SelectCommandBuilder.BuildSelectByKey(key, null, LockType.None);
        return key.SelectByKeyCommand;
      }
      return SelectCommandBuilder.BuildSelectByKey(key, mask, lockType);
    }

    public void ScheduleLinqNonQuery<TEntity>(IQueryable baseQuery, LinqOperation op,
                             CommandSchedule schedule = CommandSchedule.TransactionEnd) {
      Util.Check(baseQuery is EntityQuery, "query parameter should an EntityQuery.");
      var model = Context.App.Model;
      var targetEnt = model.GetEntityInfo(typeof(TEntity));
      Util.Check(targetEnt != null, "Generic parameter {0} is not an entity registered in the Model.", typeof(TEntity));
      var command = new DynamicLinqCommand(this, LinqCommandSource.Dynamic, op, baseQuery.Expression, targetEnt);
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


  } //class
}
