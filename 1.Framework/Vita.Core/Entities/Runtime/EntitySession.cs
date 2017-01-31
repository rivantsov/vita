using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Common;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Services;
using Vita.Entities.Linq;
using Vita.Data;
using Vita.Entities.Locking;

namespace Vita.Entities.Runtime {

  [Flags]
  public enum EntitySessionOptions {
    None = 0,
    ReadOnly = 1,
    Concurrent = 1 << 1, //allowed only for readonly sessions
    DisableStoredProcs = 1 << 2,
    DisableBatch = 1 << 3,
    Default = None,
  }



  /// <summary> Represents a live connection to the database with tracking of loaded and changed/added/deleted entities. </summary>
  /// <remarks>This class provides methods for data access: reading, updating, deleting entitites. </remarks>
  public partial class EntitySession : IEntitySession  {
    public readonly OperationContext Context;
    public readonly MemoryLog LocalLog;
    public EntitySessionOptions Options;

    public readonly bool IsReadOnly;
    public readonly bool IsSecureSession;
    public bool LogDisabled; //true for background save service - which mostly saves logs
    public bool CacheDisabled; //disable cache temporarily

    public readonly EntityRecordWeakRefTable RecordsLoaded;
    public readonly IList<EntityRecord> RecordsChanged;
    public readonly IList<IPropertyBoundList> ListsChanged;
    public readonly HashSet<EntityRecord> RecordsToClearLists;
    public readonly List<ScheduledLinqCommand> ScheduledCommands;

    // Reusable connection object
    public DataConnection CurrentConnection {
      get { return _currentConnection; }
      set {
        if(value == _currentConnection)
          return; 
        _currentConnection = value; 
        if (_currentConnection != null)
          Context.RegisterDisposable(_currentConnection);
      }
    } DataConnection _currentConnection; 

    //last executed LINQ query; can be retrieved using EntityHelper.GetLastQuery(this session)
    public LinqCommand LastLinqCommand { get; private set; }
    // last executed DbCommand, use EntityHelper.GetLastCommand(this session) to retrieve it.
    public System.Data.IDbCommand LastCommand { get; internal set; }

    public DateTime TransactionDateTime { get; private set; }
    public long TransactionStart { get; private set; }
    public Guid NextTransactionId; // copied into ITransactionLog.Id
    public int TransactionRecordCount; 

    private ITimeService _timeService;
    private EntityAppEvents _appEvents;
    IOperationLogService _operationLog; 

    #region constructor
    public EntitySession(OperationContext context, EntitySessionOptions options = EntitySessionOptions.Default) {
      Context = context;
      this.Options = options;
      IsSecureSession = this is Vita.Entities.Authorization.SecureSession;
      _appEvents = Context.App.AppEvents;
      this.LocalLog = Context.LocalLog;
      // Multithreaded sessions must be readonly
      var isConcurrent = Options.IsSet(EntitySessionOptions.Concurrent);
      IsReadOnly = Options.IsSet(EntitySessionOptions.ReadOnly) || isConcurrent;
      _timeService = Context.App.GetService<ITimeService>();
      _operationLog = Context.App.GetService<IOperationLogService>();
      RecordsLoaded = new EntityRecordWeakRefTable(isConcurrent);
      // These two lists are not used in Readonly sessions
      if (!IsReadOnly) {
        RecordsChanged = new List<EntityRecord>();
        ListsChanged = new List<IPropertyBoundList>();
        RecordsToClearLists = new HashSet<EntityRecord>();
        ScheduledCommands = new List<ScheduledLinqCommand>(); 
      }
      _appEvents.OnNewSession(this);
      //These might be reset in SaveChanges
      NextTransactionId = Guid.NewGuid(); 
      TransactionDateTime = _timeService.UtcNow;
      TransactionStart = _timeService.ElapsedMilliseconds; 
    }
    #endregion

    public override string ToString() {
      return "EntitySession(" + Context + ")";
    }

    #region IEntitySession Methods ===========================================================================================

    OperationContext IEntitySession.Context {
      get { return Context; } 
    }

    public virtual TEntity NewEntity<TEntity>() where TEntity: class {
      CheckNotReadonly();
      var entityInfo = GetEntityInfo(typeof(TEntity));
      Util.Check(entityInfo.Kind != EntityKind.View, "Entity {0} is a view, cannot create new entity.", typeof(TEntity));
      var newRec = NewRecord(entityInfo);
      return (TEntity) (object) newRec.EntityInstance;
    }

    public virtual TEntity GetEntity<TEntity>(object primaryKeyValue, LoadFlags flags = LoadFlags.Default) where TEntity : class {
      Util.Check(primaryKeyValue != null, "Session.GetEntity<{0}>(): primary key may not be null.", typeof(TEntity));
      var entityInfo = GetEntityInfo(typeof(TEntity));
      Util.Check(entityInfo.Kind != EntityKind.View, "Cannot use session.GetEntity<TEntity>(PK) method for views. Entity: {0}.", typeof(TEntity));
      //Check if it is an entity key object; if not, it is a "value" (or values) of the key
      var pkType = primaryKeyValue.GetType();
      EntityKey pk;
      if (pkType == typeof(EntityKey)) {
        pk = (EntityKey)primaryKeyValue;
      } else {
        //try array of objects; if not, it is a single value, turn it into array
        object[] pkValues = pkType == typeof(object[]) ? (object[]) primaryKeyValue : new object[] { primaryKeyValue };
        pk = EntityKey.CreateSafe(entityInfo.PrimaryKey, pkValues);
      }
      var rec = GetRecord(pk, flags);
      if (rec != null)
        return (TEntity)(object) rec.EntityInstance;
      return default(TEntity);
    }

    public virtual void DeleteEntity<TEntity>(TEntity entity) where TEntity : class {
      CheckNotReadonly();
      var record = EntityHelper.GetRecord(entity);
      DeleteRecord(record);
    }

    public virtual bool CanDeleteEntity<TEntity>(TEntity entity, out Type[] blockingEntities) where TEntity : class {
      var record = EntityHelper.GetRecord(entity);
      Util.Check(record != null, "CanDeleteEntity: entity parameter is not an Entity.");
      return CanDeleteRecord(record, out blockingEntities);
    }

    public virtual IList<TEntity> GetEntities<TEntity>(int skip = 0, int take = int.MaxValue) where TEntity : class {
      Util.Check(skip >= 0 && take >= 0, "Invalid paging arguments: skip={0}, take=(1}", skip, take);
      var entityInfo = GetEntityInfo(typeof(TEntity));
      Util.Check(entityInfo != null, "Entity {0} not registered with the Model.", typeof(TEntity));
      bool withPaging = skip != 0 || take < int.MaxValue;
      EntityCommand cmd; 
      object[] args; 
      if (withPaging) {
        cmd = entityInfo.CrudCommands.SelectAllPaged;
        args = new object[] {skip, take};
      } else {
        cmd = entityInfo.CrudCommands.SelectAll;
        args = null; 
      }
      var records = ExecuteSelect(cmd, args);
      return ToEntities<TEntity>(records); 
    }

    public IList<TEntity> GetEntities<TEntity>(IEnumerable keyValues) where TEntity : class {
      if (IsEmpty(keyValues)) //protect against empty list, most servers do not like 'x IN ()' - with empty list
        return new List<TEntity>(); 
      var entityInfo = GetEntityInfo(typeof(TEntity));
      Util.Check(entityInfo != null, "Entity {0} not registered with the Model.", typeof(TEntity));
      EntityCommand cmd = entityInfo.CrudCommands.SelectByPrimaryKeyArray;
      Util.Check(cmd != null, "Command SelectByPrimaryKeyArray is not defined for entity {0}.", entityInfo.Name);
      var args = new object[] { keyValues };
      var records = ExecuteSelect(cmd, args);
      return ToEntities<TEntity>(records);
    }
    
    private bool IsEmpty(IEnumerable e) {
      var iEnum = e.GetEnumerator();
      return !iEnum.MoveNext();
    }

    public void CancelChanges() {
      CheckNotReadonly(); 
      foreach (var rec in RecordsChanged) {
        rec.EntityInfo.Events.OnChangesCanceled(rec);
        rec.CancelChanges();
      }
      RecordsChanged.Clear();
      ListsChanged.Clear(); 
    }
    public bool HasChanges() {
      return this.RecordsChanged.Count > 0 || this.ScheduledCommands.Count > 0; 
    }

    public virtual void SaveChanges() {
      CheckNotReadonly();
      TransactionDateTime = _timeService.UtcNow;
      TransactionStart = _timeService.ElapsedMilliseconds;
      TransactionRecordCount = RecordsChanged.Count; 
      //Invoke on Saving first, to let auto values to be filled in, before validation
      OnSaving();
      if(!_validationDisabled)
        ValidateChanges();
      try {
        TransactionRecordCount = RecordsChanged.Count;
        SubmitChanges();
        OnSaved();
        RecordsChanged.Clear();
        ListsChanged.Clear();
      } catch (Exception ex) {
        OnSaveAborted();
        _appEvents.OnError(this, ex);
        throw;
      }
      NextTransactionId = Guid.NewGuid();
      _transationTags = null; 
    }//method

    protected virtual void SubmitChanges() {
      if (this.CurrentConnection == null && !this.HasChanges()) 
        return;
      EntityApp app; 
      if (this.RecordsChanged.Count > 0) {
        // account for LinkedApps
        app = this.RecordsChanged[0].EntityInfo.Module.App;
      } else
        // If we have shared connection, we still need to call database to handle closing it and possibly committing transaction
        app = this.Context.App;
      var ds = app.DataAccess.GetDataSource(this.Context);
      ds.SaveChanges(this); 
    }

    public IQueryable<TEntity> EntitySet<TEntity>() where TEntity : class {
      return CreateEntitySet<TEntity>(LockOptions.None); 
    }

    internal protected virtual IQueryable<TEntity> CreateEntitySet<TEntity>(LockOptions lockOptions) where TEntity : class {
       var entInfo = GetEntityInfo(typeof(TEntity));
      var prov = new EntityQueryProvider(this); 
      var entSet = new EntitySet<TEntity>(prov, lockOptions);
      //Check filters
      var pred = Context.QueryFilter.GetPredicate<TEntity>();
      if(pred != null)
        return entSet.Where(pred.Where);
      else 
        return entSet;
    }

    #endregion 

    #region Record manipulation methods
    public virtual EntityRecord GetRecord(EntityKey primaryKey, LoadFlags flags = LoadFlags.Default) {
      if (primaryKey == null || primaryKey.IsNull())
        return null; 
      var record = GetLoadedRecord(primaryKey);
      if (record != null) {
        if (record.Status == EntityStatus.Stub && flags.IsSet(LoadFlags.Load)) record.Reload(); 
        return record; 
      }
      // TODO: review returning Record stub in the context of SecureSession. 
      // We might have security hole here if user is not authorized to access the record  
      if (flags.IsSet(LoadFlags.Stub))
        return CreateStub(primaryKey);
      if (!flags.IsSet(LoadFlags.Load))
        return null; 
      //Otherwise, load it
      var entityInfo = primaryKey.KeyInfo.Entity;
      var recs = this.ExecuteSelect(entityInfo.CrudCommands.SelectByPrimaryKey, primaryKey.Values);
      record = recs.Count > 0 ? recs[0] : null;
      return record; 
    }

    public virtual IList<EntityRecord> GetChildRecords(EntityRecord parent, EntityMemberInfo refMember) {
      Util.Check(refMember.Kind == MemberKind.EntityRef, 
        "Invalid refMember parameter ({0}) - should be an Entity reference member(property).", refMember.MemberName);
      if (parent.Status == EntityStatus.New)
        return new List<EntityRecord>(); 
      return this.ExecuteSelect(refMember.ReferenceInfo.FromKey.SelectByKeyCommand, parent.PrimaryKey.Values);
    }

    public virtual EntityRecord NewRecord(EntityInfo entityInfo) {
      var record = new EntityRecord(entityInfo, EntityStatus.New);
      record = Attach(record);
      this.Context.App.EntityEvents.OnNew(record);
      record.EntityInfo.Events.OnNew(record);
      return record;
    }

    public void DeleteRecord(EntityInfo entity, EntityKey primaryKeyValue) {
      var rec = GetRecord(primaryKeyValue, LoadFlags.Stub);
      if (rec == null) return;
      DeleteRecord(rec);
    }

    public virtual bool CanDeleteRecord(EntityRecord record, out Type[] blockingEntities) {
      Util.Check(record != null, "CanDeleteRecord: record parameter may not be null.");
      blockingEntities = null; 
      var entInfo = record.EntityInfo;
      var blockingTypeSet = new HashSet<Type>();
      //check all external ref members
      foreach (var refMember in entInfo.IncomingReferences) {
        // if it is cascading delete, this member is not a problem
        if (refMember.Flags.IsSet(EntityMemberFlags.CascadeDelete)) continue;
        var countCmdInfo = refMember.ReferenceInfo.CountCommand;
        var countCmd = new LinqCommand(countCmdInfo, refMember.ReferenceInfo.ToKey.Entity, new object[] {this, record.EntityInstance});
        var count = (int)ExecuteLinqCommand(countCmd);
        if (count > 0)
          blockingTypeSet.Add(refMember.Entity.EntityType);
      }
      if (blockingTypeSet.Count == 0)
        return true;
      blockingEntities = blockingTypeSet.ToArray();
      return false; 
    }

    public virtual void DeleteRecord(EntityRecord record) {
      Util.Check(record != null, "DeleteRecord: record parameter may not be null.");
      if(record.Status == EntityStatus.New) {
        RecordsChanged.Remove(record);
        record.Status = EntityStatus.Fantom;
      }
      record.MarkForDelete();
      record.EntityInfo.Events.OnDeleting(record);
      this.Context.App.EntityEvents.OnDeleting(record);
    }

    public virtual EntityRecord Attach(EntityRecord record) {
      if (record == null) return null;
      if (record.IsAttached && record.Session == this) 
        return record; //already attached
      record.Session = this;
      if (record.EntityInfo.Kind == EntityKind.View && record.EntityInfo.PrimaryKey == null) { // for view records
        record.Status = EntityStatus.Loaded; 
        return record;
      }
      //Actually attach
      record.IsAttached = true; 
      var oldRecord = record; //might switch to the record already loaded in session
      //Add record to appropriate collection
      switch (record.Status) {
        case EntityStatus.Loading:
        case EntityStatus.Stub:
        case EntityStatus.Loaded:
          record.Status = EntityStatus.Loaded;
          oldRecord = RecordsLoaded.Add(record); //might return existing record
          if (oldRecord != record) { // it is brand new record just loaded
            oldRecord.CopyOriginalValues(record);
            oldRecord.ClearEntityRefValues();
          }
          record.EntityInfo.Events.OnLoaded(record);
          Context.App.EntityEvents.OnLoaded(record);
          break; 
        case EntityStatus.New:
          RecordsChanged.Add(record);
          break;
        case EntityStatus.Deleting:
        case EntityStatus.Modified:
          oldRecord = RecordsLoaded.Add(record);
          RecordsChanged.Add(oldRecord);
          break; 
      }
      /*
      //Just in case, if 'record' is freshly loaded from database, and we have already the same 'attachedRecord' in session, 
      // refresh the column values
      if (attachedRecord != record) {
        attachedRecord.CopyOriginalValues(record);
        attachedRecord.ClearEntityRefValues(); 
      }
       */ 
      return oldRecord;
    }

    public void NotifyRecordStatusChanged(EntityRecord record, EntityStatus oldStatus) {
      var newStatus = record.Status; 
      if (newStatus == oldStatus)
        return; 
      //check new status
      switch (newStatus) {
        case EntityStatus.Loaded:
          if(oldStatus == EntityStatus.New)
            RecordsLoaded.Add(record);
          break;
        case EntityStatus.New:
        case EntityStatus.Modified:
        case EntityStatus.Deleting:
          CheckNotReadonly(); 
          RecordsChanged.Add(record);
          break; 
        case EntityStatus.Fantom:
          this.RecordsLoaded.TryRemove(record.PrimaryKey);
          break; 
      }
    }

    public virtual IList<EntityRecord> ExecuteSelect(EntityCommand command, params object[] args) {
      try {
        var targetApp = command.TargetEntityInfo.Module.App;
        var ds = targetApp.DataAccess.GetDataSource(this.Context);
        var result = ds.ExecuteSelect(this, command, args);
        _appEvents.OnExecutedSelect(this, command);
        return result; 
      } catch(Exception ex) {
        ex.AddValue("command-name", command.CommandName);
        ex.AddValue("args", args);
        throw;
      }
    }

    #endregion

    #region Record Get/Set Value
    // Will be overridden SecureSession
    public virtual object RecordGetMemberValue(EntityRecord record, EntityMemberInfo member) {
      return member.GetValueRef(record, member);
    }
    public virtual void RecordSetMemberValue(EntityRecord record, EntityMemberInfo member, object value) {
      member.SetValueRef(record, member, value);
    }

    #endregion

    #region OnSave methods
    // We might have new entities added on the fly, as we fire events. For example, ChangeTracking hooks to EntityStore and it adds new IChangeTrackEntries.
    // We need to make sure that for all added records we fire the Entity's OnSaving event. (All Auto attributes are processed in this event). 
    // That's the reason for tracking 'done-Counts' and calling the invoke loop twice
    protected virtual void OnSaving() {
      _appEvents.OnSavingChanges(this);
      // We need to invoke OnSaving event for each record and Notify each changed entity list. The event handlers may add extra records, 
      // so we may need to repeat the process for extra records.
      int startListChangedIndex = 0;
      int startRecordsChangedIndex = 0;
      while(true) {
        var oldListsChangedCount = ListsChanged.Count;
        var oldRecordsChangedCount = RecordsChanged.Count;
        //Notify all changed lists - these may add/delete records (many-to-many link records)
        for(int i = startListChangedIndex; i < oldListsChangedCount; i++)
          ListsChanged[i].Notify(BoundListEventType.SavingChanges);
        //Saving event handlers may add/remove changed records, so we need to use 'for-i' loop here
        for(int i = startRecordsChangedIndex; i < oldRecordsChangedCount; i++) {
          var rec = RecordsChanged[i];
          rec.EntityInfo.SaveEvents.OnSavingChanges(rec);
        }
        //Check if we are done, or we need to do it again
        if(oldListsChangedCount == ListsChanged.Count && oldRecordsChangedCount == RecordsChanged.Count)
          return;
        startListChangedIndex = oldListsChangedCount;
        startRecordsChangedIndex = oldRecordsChangedCount;
      }
    }

    protected void OnSaved() {
      //Notify all changed lists
      foreach(var list in ListsChanged)
        list.Notify(BoundListEventType.SavedChanges);
      //Process records
      for(int i = 0; i < this.RecordsChanged.Count; i++) {
        var rec = RecordsChanged[i];
        rec.CommitChanges();
        rec.EntityInfo.SaveEvents.OnSavedChanges(rec);
      }
      // Clear lists
      foreach(var rec in this.RecordsToClearLists)
        rec.ClearTransientValues();
      this.RecordsToClearLists.Clear(); 
      //fire events
      _appEvents.OnSavedChanges(this);
    }

    protected void OnSaveAborted() {
      // we need to iterate down, because we my eliminate records from the list
      for (int i = this.RecordsChanged.Count - 1; i >= 0; i--) {
        var rec = RecordsChanged[i];
        rec.EntityInfo.SaveEvents.OnSaveChangesAborted(rec);
        if (rec.EntityInfo.Flags.IsSet(EntityFlags.DiscardOnAbourt)) {
          // Dynamic records are created on the fly when app submits changes (ex: trans logs). If trans aborted, they must be discarded
          RecordsChanged.RemoveAt(i);
          RecordsLoaded.TryRemove(rec.PrimaryKey); 
        }
      }
      this.RecordsToClearLists.Clear(); 
      _appEvents.OnSaveChangesAborted(this);
    }
    #endregion

    #region private methods
    private EntityRecord CreateStub(EntityKey primaryKey) {
      var rec = new EntityRecord(primaryKey);
      rec.Session = this; 
      RecordsLoaded.Add(rec);
      return rec;
    }
    protected virtual EntityRecord GetLoadedRecord(EntityKey primaryKey) {
      if (primaryKey == null)
        return null;
      //1. Look in the cache
      //var entityInfo = primaryKey.Key.Entity;
      // var rec = Database.Cache.Lookup(entityInfo.EntityType, primaryKey);
      // if(rec != null) return rec;
      //2. Look in the group - if it was previously loaded
      var rec = RecordsLoaded.Find(primaryKey);
      // additionally check RecordsChanged; New records are not included in RecordsLoaded because they do not have PrimaryKes set (yet)
      if (rec == null && RecordsChanged != null && RecordsChanged.Count > 0)
        rec = FindNewRecord(primaryKey); 
      return rec;
    }

    private EntityRecord FindNewRecord(EntityKey primaryKey) {
      var ent = primaryKey.KeyInfo.Entity;
      for(int i=0; i < RecordsChanged.Count; i++) { //for-i loop is faster
        var rec = RecordsChanged[i]; 
        if (rec.Status != EntityStatus.New || rec.EntityInfo != ent) continue; 
        //we found new record of matching entity type. Check keys match. First check if key is loaded
        if (!rec.KeyIsLoaded(primaryKey.KeyInfo)) continue; 
        if (primaryKey.Equals(rec.PrimaryKey)) 
          return rec; 
      }
      return null; 
    }
    
    public virtual IList<TEntity> ToEntities<TEntity>(IEnumerable<EntityRecord> records) {
      var list = new List<EntityBase>();
      foreach (EntityRecord rec in records)
        list.Add(rec.EntityInstance);
      var entList = new ObservableEntityList<TEntity>(list); 
      return entList;
    }


    protected internal EntityInfo GetEntityInfo(Type entityType) {
      return Context.App.Model.GetEntityInfo(entityType, throwIfNotFound: true);
    }

    private static bool IsSet(LoadFlags options, LoadFlags option) {
      return (options & option) != 0;
    }
    #endregion

    #region Dynamic query execution: ExecuteDynamicQuery
    public virtual object ExecuteLinqCommand(LinqCommand command) {
      this.LastLinqCommand = command;
      var cmdInfo = LinqCommandAnalyzer.Analyze(Context.App.Model, command);
      if (command.Kind == LinqCommandKind.DynamicSql)
        command.EvaluateLocalValues(this);
      //account for LinkedApps - get any entity involved in query and get the app it is registered in
      EntityApp targetApp = this.Context.App;
      if (cmdInfo.EntityTypes.Count > 0) {
        var someType = cmdInfo.EntityTypes[0];
        var someEntInfo = GetEntityInfo(someType);
        Util.Check(someEntInfo != null, "Type {0} is not a registered entity, cannot execute the query.", someType);
        targetApp = someEntInfo.Module.App; 
      }
      var ds = targetApp.DataAccess.GetDataSource(this.Context);
      var result = ds.ExecuteLinqCommand(this, command);
      //fire events
      switch(command.CommandType) {
        case LinqCommandType.Select: 
          _appEvents.OnExecutedQuery(this, command); break;
        default: 
          _appEvents.OnExecutedNonQuery(this, command);
          NextTransactionId = Guid.NewGuid();
          break; 
      }
      if (command.Info.Includes.Count > 0 || Context.HasIncludes())
        IncludeQueryHelper.RunIncludeQueries(this, command, result);
      return result;
    }

    #endregion

    #region Validation
    public virtual void ValidateChanges() {
      foreach(var rec in RecordsChanged)
        ValidateRecord(rec);
      Context.ClientFaults.Throw();
    }

    public bool ValidateRecord(EntityRecord record) {
      record.ValidationErrors = null; //reset
      if(record.Status == EntityStatus.Deleting)
        return true;
      record.EntityInfo.Events.OnValidatingChanges(record);
      foreach(var member in record.EntityInfo.Members) {
        switch(member.Kind) {
          case MemberKind.Column:
            // We do not validate columns that are not updated.
            // We do not validate foreign keys - instead, we validate corresponding EntityRef members.
            if(member.Flags.IsSet(EntityMemberFlags.NoDbUpdate | EntityMemberFlags.NoDbInsert | EntityMemberFlags.ForeignKey))
              continue;
            ValidateValueMember(record, member);
            continue;
          case MemberKind.EntityRef:
            ValidateEntityRefMember(record, member);
            continue;
          case MemberKind.EntityList:
          case MemberKind.Transient:
            continue; //do nothing
        }
      }//foreach member
      if(!record.IsValid) {
        record.EntityInfo.Events.OnValidationFailed(record);
      }
      return record.IsValid;
    }

    private void ValidateEntityRefMember(EntityRecord record, EntityMemberInfo member) {
      bool nullable = member.Flags.IsSet(EntityMemberFlags.Nullable);
      if(nullable)
        return; //nothing to check
      var isNull = record.KeyIsNull(member.ReferenceInfo.FromKey);
      if(isNull)
        record.AddValidationError(ClientFaultCodes.ValueMissing, ValidationMessages.ValueMissing, new object[] { member.MemberName }, 
          member.MemberName);
    }


    private void ValidateValueMember(EntityRecord record, EntityMemberInfo member) {
      bool nullable = member.Flags.IsSet(EntityMemberFlags.Nullable);
      var value = record.GetValue(member);
      // Checking non-nullable fields; note that identity field for new records is initialized with temp value (in ValueBox)
      if(!nullable) {
        if(member.DataType == typeof(string)) {
          // Treat empty string as null
          if(string.IsNullOrEmpty((string)value)) {
            record.AddValidationError(ClientFaultCodes.ValueMissing, ValidationMessages.ValueMissing, new object[] { member.MemberName },
              member.MemberName);
          }
        } else {
          if(value == null || value == DBNull.Value)
          record.AddValidationError(ClientFaultCodes.ValueMissing, ValidationMessages.ValueMissing, new object[] { member.MemberName }, 
            member.MemberName);
        }
      }
      //Check string size
      if(member.DataType == typeof(string) && member.Size > 0) {
        var strValue = (string)value;
        if(strValue != null && strValue.Length > member.Size && member.Flags.IsSet(EntityMemberFlags.AutoValue)) {
          strValue = strValue.Substring(member.Size);
          record.SetValueDirect(member, strValue);
        } else {
          if(value != null && strValue.Length > member.Size)
            record.AddValidationError(ClientFaultCodes.ValueTooLong, ValidationMessages.ValueTooLong, new object[] { member.MemberName, member.Size }, 
              member.MemberName, strValue);
        }
      }
    }
    #endregion

    #region Misc methods

    public object EvaluateLambdaParameter(ParameterExpression parameter) {
      if (parameter.Type == typeof(IEntitySession) || parameter.Type == typeof(ISecureSession))
        return this;
      if (parameter.Type == typeof(OperationContext))
        return this.Context;
      var name = parameter.Name.ToLowerInvariant();
      switch (name) {
        case "userid": return Context.User.UserId;
        case "altuserid": return Context.User.AltUserId;
        default:
          object value = null; 
          Util.Check(Context.TryGetValue(parameter.Name, out value),
                      "Filter expression parameter {0} not set in OperationContext.", parameter.Name);
          var prmType = parameter.Type;
          if (value.GetType() == prmType)
            return value;
          value = ConvertHelper.ChangeType(value, prmType);
          return value;
      }
    }


    public void SetLastCommand(System.Data.IDbCommand command) {
      LastCommand = command;
    }

    // Used for unit testing, to see how validation errors propagate to client. 
    // We disable validation on the client, while keep it enabled  on the server
    bool _validationDisabled;
    public void EnableValidation(bool enable) {
      _validationDisabled = !enable;
    }

    private void CheckNotReadonly() {
      Util.Check(!IsReadOnly, "Cannot modify records, session is readonly.");
    }

    private void CheckCommandArgsCount(EntityCommand command, object[] args) {
      var entCommand = command as EntityCommand;
      var paramCount = entCommand.Parameters.Count;
      var argCount = args == null ? 0 : args.Length;
      Util.Check(paramCount == argCount, "Command {0}: Parameters count in command definition ({1}) and args count ({2}) do not match.",
        command.CommandName, paramCount, argCount);
    }

    public void AddLogEntry(OperationLogEntry entry) {
      if(LogDisabled && !(entry is ErrorLogEntry))
        return; 
      LocalLog.AddEntry(entry);
      if(_operationLog != null)
        _operationLog.Log(entry); 
    }
    #endregion

    #region TransactionTags
    // free-form strings associated with next save transaction; cleared after SaveChanges()
    StringSet _transationTags;
    public StringSet TransactionTags {
      get {
        _transationTags = _transationTags ?? new StringSet();
        return _transationTags;
      }
    }
    #endregion 

    //this method is defined for conveniences; only derived method in SecureSession does real elevation
    // But we want to be able to call this method without checking if we have secure session or not. 
    public virtual IDisposable ElevateRead() {
      return new DummyDisposable();
    }

    public virtual IDisposable WithNoCache() {
      return new CacheDisableToken(this);
    }

    #region nested Disposable tokens
    class CacheDisableToken : IDisposable {
      bool _wasDisabled; 
      EntitySession _session;
      public CacheDisableToken(EntitySession session) {
        _session = session; 
        _wasDisabled = session.CacheDisabled;
        session.CacheDisabled = true; 
      }
      public void Dispose() {
        _session.CacheDisabled = _wasDisabled; 
      }
    }
    class DummyDisposable : IDisposable {
      public void Dispose() { }
    }
    #endregion 

  }//class

}//namespace
