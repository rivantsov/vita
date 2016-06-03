using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization {
  using Runtime;
  using Vita.Entities.Linq;
  using System.Collections;
  using Vita.Entities.Locking;
  //using Vita.Data;

  public class SecureSession : EntitySession, ISecureSession {

    #region ISecureSession members
    public ReadAccessLevel DemandReadAccessLevel {
      get { return _demandReadAccessLevel; }
      set {
        _demandReadAccessLevel = value;
        _demandReadAccessType = _demandReadAccessLevel.ToAccessType(); 
      } 
    } ReadAccessLevel _demandReadAccessLevel;
    private AccessType _demandReadAccessType; // Equiv of DemandReadAccessLevel, set automatically when we set DemandReadAccesLevel

    public DenyReadActionType DenyReadAction { get; set; }
    #endregion
    
    public SecureSession(OperationContext context) : base(context) {
      Util.Check(Context.User.Authority != null, "User.Authority must be specified in the operation context for secure session.");
      DemandReadAccessLevel = ReadAccessLevel.Peek;
      DenyReadAction = DenyReadActionType.Throw;
    }

    public override string ToString() {
      return "SecureSession(" + Context.User + ")";
    }

    public bool IsAccessAllowed<TEntity>(AccessType accessType) {
      Util.Check(Context.User.Authority != null, "User Authority is not set.");
      var entInfo = GetEntityInfo(typeof(TEntity));
      var access = Context.User.Authority.GetEntityTypePermissions(Context, entInfo);
      //Check each action type separately
      if(accessType.IsSet(AccessType.CreateStrict) && !access.AccessTypes.IsSet(AccessType.CreateStrict))
        return false;
      if(accessType.IsSet(AccessType.Peek) && !access.Peek.Allowed())
        return false;
      if(accessType.IsSet(AccessType.ReadStrict) && !access.ReadStrict.Allowed())
        return false;
      if(accessType.IsSet(AccessType.UpdateStrict) && !access.UpdateStrict.Allowed())
        return false;
      if(accessType.IsSet(AccessType.DeleteStrict) && !access.AccessTypes.IsSet(AccessType.DeleteStrict))
        return false;
      return true;
    }

    //Note: we override mostly specific low-level methods we need to intercept
    protected override EntityRecord GetLoadedRecord(EntityKey primaryKey) {
      var rec = base.GetLoadedRecord(primaryKey);
      if (rec == null || ReadUnrestricted)
        return rec;
      if (CheckRecordAccess(rec, _demandReadAccessType)) // throws if DenyReadMode is Throw
        return rec;
      return null; //if CheckRecordAccess did not throw  
    }

    public override EntityRecord NewRecord(EntityInfo entityInfo) {
      UserEntityTypePermission typePermissions;
      //CheckEntityAccess throws only if DenyReadMode is Throw; but we throw anyway in NewRecord
      if (!CheckEntityAccess(entityInfo, AccessType.CreateStrict, out typePermissions))
        AccessDenied(AccessType.Create, entityInfo, typePermissions); 
      var rec = base.NewRecord(entityInfo);
      rec.UserPermissions = typePermissions; //initially
      return rec; 
    }

    public override void DeleteRecord(EntityRecord record) {
      if (!CheckRecordAccess(record, AccessType.DeleteStrict))
        return; //CheckRecordAccess throws if DenyReadMode is Throw
      base.DeleteRecord(record);
    }
    
    internal protected override IQueryable<TEntity> CreateEntitySet<TEntity>(LockOptions lockOptions) {
      QueryPredicate<TEntity> authFilter = null;
      // We allow to execute LINQ queries in elevated mode
      if(!ReadUnrestricted) {
        //Check access rights
        var entInfo = GetEntityInfo(typeof(TEntity));
        UserEntityTypePermission typePermissions;
        // CheckEntityAccess will throw AccessDenied if DenyReadAction is Throw; if it is Filter, we just return empty list
        if(!CheckEntityAccess(entInfo, _demandReadAccessType, out typePermissions))
          return new List<TEntity>().AsQueryable(); //return empty list
        //read predicate filter from Authority
        authFilter = this.Context.User.Authority.GetQueryFilter<TEntity>();
      }
      IQueryable<TEntity> entSet = base.CreateEntitySet<TEntity>(lockOptions);
      //base entSet already includes predicate from Context.QueryFilter (if there is one)
      // We now add predicate from authorization
      if(authFilter == null)
        return entSet;
      else
        return entSet.Where(authFilter.Where);
    }

    public override IList<EntityRecord> ExecuteSelect(EntityCommand command, params object[] args) {
      if (ReadUnrestricted)
        return base.ExecuteSelect(command, args);
      //Non-elevated - first check entity type-level access
      var entCommand = command as EntityCommand; 
      UserEntityTypePermission typePermissions;
      if (!CheckEntityAccess(entCommand.TargetEntityInfo, entCommand.AccessType, out typePermissions))
        return new List<EntityRecord>(); //return empty list, if exception is not thrown by CheckEntityAccess
      //execute query
      var records = base.ExecuteSelect(command, args);
      //If there are record-level permissions, filter records
      if (typePermissions.HasFilter)
        return FilterRecords(records);
      //Otherwise, set the rights on each record same as entity type access
      foreach (var rec in records)
        rec.UserPermissions = typePermissions;
      return records;
    }

    public override bool CanDeleteRecord(EntityRecord record, out Type[] blockingEntities) {
      // checking related records may require querying entities that may not be available to the current user.
      using (new UnrestrictedReadToken(this)) {
        return base.CanDeleteRecord(record, out blockingEntities);
      }
    }

    // Note: Earlier implementation was doing authorization check in Attach method - but this does not work well, this is too early
    // Instead, checking authorization in ExecuteQuery 
    public override object ExecuteLinqCommand(LinqCommand command) {
      var result = base.ExecuteLinqCommand(command);
      if (result == null)
        return null;
      if (command.CommandType != LinqCommandType.Select)
        return result; 
      switch(command.Info.ResultShape) {
        case QueryResultShape.Entity:
          var rec = EntityHelper.GetRecord(result); 
          if (!CheckRecordAccess(rec, AccessType.Peek))
            return null;
          return result; 
        case QueryResultShape.EntityList:
          // Note that result list is a typed List<TEntity>; we need to filter it. To avoid trouble of creating another typed list, 
          // we filter elements 'in place', iterating it in reverse order
          var list = result as System.Collections.IList;
          for(int i = list.Count - 1; i >= 0; i--) {
            var ent = list[i];
            var rec2 = EntityHelper.GetRecord(ent); 
            if (rec2 == null || !CheckRecordAccess(rec2, AccessType.Peek))
              list.RemoveAt(i); 
          }
          return result; 
        default:
          // if it is auto type or list of auto types, we return it as-is. If auto-object has Entities in properties, they will be there, 
          //  even if authorization does not allow the user to access them; 
          // but as soon as the code tries to access properties of these entities, authorization will throw AccessDenied
          return result; 
      }
    }


    protected override void SubmitChanges() {
      // Access rights might change due to record field changes.
      // One example from books module: user creates BookReview record - he is granted the rights to do so. 
      // But then the code assigns other user as review owner - so user tries to create a review mascarading 
      // as another user. This situation must be caught here. 
      foreach (var rec in RecordsChanged) {
        rec.UserPermissions = null; //reset rights and force new rights evaluation
        CheckRecordAccess(rec, rec.Status.GetRequiredAccessType());
        // verify write permissions for modified fields
        if (rec.UserPermissions != null && rec.UserPermissions.UpdateStrict.Allowed())
          VerifyModifiedValues(rec); 
      }
      base.SubmitChanges();
    }

    protected override void OnSaving() {
      // OnSave handlers may contain methods that travers internal record links. 
      // For example, ChangeLog might try to find GroupId for records, and needs to access props/entities that may not be available 
      // to the current user. So we need to use elevated mode here to disable access rights checks.
      using (new UnrestrictedReadToken(this)) {
        base.OnSaving();
      }
    }
    public override void ValidateChanges() {
      // Validation methods might access data that is not normally available to the current user. So we need to use elevated mode here
      // to disable access rights checks.
      using (this.ElevateRead()) {
        base.ValidateChanges();
      }
    }

    public override object RecordGetMemberValue(EntityRecord record, EntityMemberInfo member) {
      if (record.Status == EntityStatus.Loading || ReadUnrestricted || !record.AccessCheckEnabled)
        return base.RecordGetMemberValue(record, member);
      //Not elevated
      if (!CheckRecordAccess(record, _demandReadAccessType))
        return member.DeniedValue;
      var rights = record.UserPermissions;
      var allowed = _demandReadAccessLevel == ReadAccessLevel.Peek ? 
        rights.Peek.Allowed(member) : rights.Peek.Allowed(member);
      if (allowed)
        return base.RecordGetMemberValue(record, member);
      //Access denied
      AccessDenied(_demandReadAccessType, record, member);
      // if AccessDenied did not throw, return default value
      return member.DeniedValue;
    }

    public override void RecordSetMemberValue(EntityRecord record, EntityMemberInfo member, object value) {
      //Check if get/set checks are enabled; they are disabled for new and loading records
      if (!record.AccessCheckEnabled) {
        base.RecordSetMemberValue(record, member, value);
        return; 
      }
      if (record.UserPermissions == null)
         record.UserPermissions = Context.User.Authority.GetRecordPermission(record);
      if (record.UserPermissions.UpdateStrict.Allowed(member))
        base.RecordSetMemberValue(record, member, value);
      else 
        AccessDenied(AccessType.UpdateStrict, record, member);
    }


    // Private utilities ========================================================================

    private bool CheckEntityAccess(EntityInfo entity, AccessType accessType, out UserEntityTypePermission permissions) {
      if(Context.User.Kind == UserKind.System || entity.Flags.IsSet(EntityFlags.BypassAuthorization)) {
        permissions = UserEntityTypePermission.Empty;
        return true; 
      }
      permissions = Context.User.Authority.GetEntityTypePermissions(Context, entity);
      if (permissions.AccessTypes.IsSet(accessType)) return true;
      var isReadAction = accessType.IsSet(AccessType.Read); 
      if (this.DenyReadAction == DenyReadActionType.Throw)
        AccessDenied(accessType, entity, permissions);
      return false; 
    }

    private bool CheckRecordAccess(EntityRecord record, AccessType accessType) {
      if (this.ReadUnrestricted)
        return true; 
      if(Context.User.Kind == UserKind.System || record.EntityInfo.Flags.IsSet(EntityFlags.BypassAuthorization)) {
        record.UserPermissions = UserRecordPermission.AllowAll;
        return true;
      }
      if(record.UserPermissions == null)
        record.UserPermissions =Context.User.Authority.GetRecordPermission(record); 
      if (record.UserPermissions.AccessTypes.IsSet(accessType)) 
        return true;
      AccessDenied(accessType, record);
      return false; 
    }

    //this overload saves us some cycles when we know already entity type access and there's no record-level access
    private bool CheckRecordAccess(EntityRecord record, AccessType accessType, UserEntityTypePermission typePermissions) {
      if (this.ReadUnrestricted)
        return true;
      if (Context.User.Kind == UserKind.System) {
        record.UserPermissions = UserRecordPermission.AllowAll;
        return true;
      }
      if(typePermissions.HasFilter)
        return CheckRecordAccess(record, accessType);
      //Otherwise, assume record rights are the same as type access
      record.UserPermissions = typePermissions;
      if (typePermissions.AccessTypes.IsSet(accessType)) 
        return true;
      AccessDenied(accessType, record);
      return false;
    }

    private IList<EntityRecord> FilterRecords(IList<EntityRecord> records) {
      var outlist = new List<EntityRecord>();
      foreach (var rec in records) {
        if (CheckRecordAccess(rec, _demandReadAccessType))
          outlist.Add(rec);
      }
      return outlist;
    }

    protected virtual void VerifyModifiedValues(EntityRecord record) {
      var rights = record.UserPermissions; 
      if (rights == null)
        return; 
      var members = record.EntityInfo.Members;
      for (int i = 0; i < members.Count; i++) {
        var member = members[i];
        if (member.Kind != MemberKind.Column) continue; 
        if (record.Modified(member) && !rights.UpdateStrict.Allowed(member)) 
          //value was modified but update is not allowed for this member
          AccessDenied(AccessType.UpdateStrict, record, member);
      }
    }

    //Throws or returns quietly depending on action
    // If access type is Read, and DenyReadMode is filter (not throw), then returns quietly.
    // Otherwise throws AuthorizationException
    private void AccessDenied(EntityCommand command, UserRecordPermission grantedRights) {
      var entCommand = command as EntityCommand;
      AccessDenied(entCommand.AccessType, entCommand.TargetEntityInfo, grantedRights);
    }

    private void AccessDenied(AccessType accessType, EntityInfo entity, UserRecordPermission grantedPermissions) {
      if (!MustThrowOnDenied(accessType)) return; 
      var msg = StringHelper.SafeFormat("Actions(s) [{0}] denied for entity {1}, authority {2}.", 
        accessType, entity, Context.User);
      var authEx = new AuthorizationException(msg, entity.EntityType, accessType, false, grantedPermissions, this);
      throw authEx;
    }

    private void AccessDenied(AccessType accessType, EntityRecord record, EntityMemberInfo member = null) {
      if (!MustThrowOnDenied(accessType)) return;
      var msg = StringHelper.SafeFormat("Actions(s) [{0}] denied for record {1}, user {2}",
        accessType, record.ToString(), Context.User);
      if (member != null)
        msg += "(Property " + member.MemberName + ")";
      var authEx = new AuthorizationException(msg, record.EntityInfo.EntityType, accessType, true, record.UserPermissions, this);
      throw authEx;
    }

    private bool MustThrowOnDenied(AccessType accessType) {
      var isReadAccess = (accessType & AccessType.Read) == accessType; // Read = Peek | ReadStrict
      return !(isReadAccess && this.DenyReadAction == DenyReadActionType.Filter);
    }

    public bool CanExecute(EntityCommand command, out UserRecordPermission grantedRights) {
      var entCommand = command as EntityCommand;
      grantedRights = Context.User.Authority.GetEntityTypePermissions(Context, entCommand.TargetEntityInfo);
      var action = entCommand.AccessType;
      return (grantedRights.AccessTypes & action) == action;
    }

    // Temp Unrestricted read - read elevation; used when evaluating data filters
    // Count of ElevateRead nested calls; if non-zero, reads are allowed without permission check. 
    private int _readElevationCount;
    internal bool ReadUnrestricted { 
      get { return _readElevationCount > 0; } 
    }
    //Must be used in "using" statements, ex: using(context.ElevateRead()) {....}
    internal void BeginReadUnrestricted() {
      _readElevationCount++;
    }
    internal void EndReadUnrestricted() {
      if(_readElevationCount > 0)
        _readElevationCount--;
    }

    public override IDisposable ElevateRead() {
      return new UnrestrictedReadToken(this);
    }

    #region Nested UnrestrictedReadToken class
    internal class UnrestrictedReadToken : IDisposable {
      SecureSession _session;
      public UnrestrictedReadToken(SecureSession session) {
        _session = session;
        _session.BeginReadUnrestricted(); 
      }
      public void Dispose() {
        _session.EndReadUnrestricted();
      }
    }
    #endregion

  }//class

}
