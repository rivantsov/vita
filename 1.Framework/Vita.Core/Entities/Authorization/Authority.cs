using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Authorization.Runtime;

namespace Vita.Entities.Authorization {

  /// <summary> A container for the runtime permission set for a user with a given role set. </summary>
  /// <remarks>Authority instances are created dynamically at runtime for authenticated users and are shared between users with the same roles.
  /// AuthorizationService holds a dynamic cache of authority instances. 
  /// </remarks>
  public class Authority {
    //A key to lookup the already created roles in authority cache
    public readonly string Key;
    public readonly List<Role> Roles;
    //compound permissions organized by entity type
    internal Dictionary<Type, UserEntityPermissionSet> EntityPermissionTable = new Dictionary<Type, UserEntityPermissionSet>();
    internal Dictionary<Type, AccessType> ObjectPermissionTable = new Dictionary<Type, AccessType>(); 
    //non-entity permission grants
    internal HashSet<GrantedPermission> NonEntityPermissionGrants = new HashSet<GrantedPermission>();
    //All dynamic grants
    internal HashSet<DynamicActivityGrant> DynamicGrants =  new HashSet<DynamicActivityGrant>();

    string _permissionsSummary;//cached value

    internal Authority(string key, IEnumerable<Role> roles) {
      Key = key;
      Roles = roles.ToList();
    }

    public override string ToString() {
      return "(Authority/" + Key + ")";
    }

    public override int GetHashCode() {
      return Key.GetHashCode();
    }

    public string GetRoleNames() {
      return string.Join(",", Roles.Select(r => r.Name));
    }

    public string GetPermissionsSummary() {
      if(_permissionsSummary != null)
        return _permissionsSummary; 
      var sep = Environment.NewLine + "  ";
      var sb = new StringBuilder();
      sb.AppendFormat("Permissions summary for Authority [{0}]", Key);//key contains list of user roles
      sb.AppendLine();
      if(this.NonEntityPermissionGrants.Count > 0) {
        sb.AppendLine("-- Non-entity permissions ----------------- ");
        sb.AppendLine(string.Join(sep, this.NonEntityPermissionGrants));
      }
      sb.AppendLine("-- Entity permissions --------------------- ");
      foreach(var de in EntityPermissionTable)
        sb.Append(de.Value.ToString());
      _permissionsSummary = sb.ToString();
      return _permissionsSummary;
    }

    public string GetPermissionsSummary(Type entityType) {
      var sep = Environment.NewLine + "  ";
      var sb = new StringBuilder();
      sb.AppendFormat("Permissions summary for Authority [{0}], Entity {1}", Key, entityType);//key contains list of user roles
      sb.AppendLine();
      UserEntityPermissionSet permSet;
      if(entityType == null || !EntityPermissionTable.TryGetValue(entityType, out permSet)) {
        sb.AppendLine("(None)");
        return sb.ToString();
      }
      sb.AppendLine("-- Entity permisssion: ");
      sb.AppendLine(permSet.ToString());
      return sb.ToString();
    }

    #region Evaluating permissions

    public UserEntityTypePermission GetEntityTypePermissions(OperationContext context, EntityInfo entity) {
      var permSet = GetEntityPermissionSet(entity.EntityType); 
      if(permSet == null)
        return UserEntityTypePermission.Empty;
      if(permSet.FixedTypePermissions != null)
        return permSet.FixedTypePermissions;
      //We need to compute permissions
      var typePerm = new UserEntityTypePermission();
      //we ignore record-level filters when computing type-level access
      foreach(var perm in permSet.ConditionalPermissions)
        if(perm.IsActive(context))
          typePerm.Merge(perm.RecordPermission);
      typePerm.HasFilter = permSet.HasFilter;
      return typePerm;
    }

    public UserRecordPermission GetRecordPermission(EntityRecord record) {
      if(!record.Session.IsSecureSession) //if it is normal session (not secure), then can read anything
        return UserRecordPermission.AllowAll;
      var entPermSet = GetEntityPermissionSet(record.EntityInfo.EntityType);
      if (entPermSet == null)
        return record.ByRefUserPermissions ?? UserRecordPermission.AllowNone;
      if(entPermSet.FixedRecordPermissions != null)
        return entPermSet.FixedRecordPermissions;
      var context = record.Session.Context;
      var ent = record.EntityInstance;
      var recordPerms = new UserRecordPermission();
      //Evaluating data filters might require access to columns that are not permitted to read by current user. 
      // So we need to elevate read temporarily
      var secSession = record.Session as SecureSession;
      using(secSession.ElevateRead()) {
        foreach(var condPerm in entPermSet.ConditionalPermissions) {
          //Check enabled (for dynamic grants), and check record filter - might be false for dynamic actions
          if(!condPerm.IsActive(context))
            continue;
          if(condPerm.FilterPredicate == null || condPerm.FilterPredicate.Evaluate(secSession, ent))
            recordPerms.Merge(condPerm.RecordPermission);
        }
        if(record.ByRefUserPermissions != null)
          recordPerms.Merge(record.ByRefUserPermissions);
        return recordPerms;
      }
    }

    public AccessType GetObjectAccess(OperationContext context, Type objectType) {
      AccessType accessType; 
      if(this.ObjectPermissionTable.TryGetValue(objectType, out accessType))
        return accessType;
      return AccessType.None;
    }

    internal UserEntityPermissionSet GetEntityPermissionSet(Type entityType, bool create = false) {
      UserEntityPermissionSet permSet;
      if(EntityPermissionTable.TryGetValue(entityType, out permSet))
        return permSet;
      permSet = null; // should be null already, but just in case
      if(create) {
        permSet = new UserEntityPermissionSet(entityType);
        EntityPermissionTable[entityType] = permSet;
      }
      return permSet; 
    }

    public QueryPredicate<TEntity> GetQueryFilter<TEntity>() {
      var entPermSet = GetEntityPermissionSet(typeof(TEntity));
      if (entPermSet == null)
        return null;
      return (QueryPredicate<TEntity>) entPermSet.QueryFilterPredicate; 
    }

    #endregion

  }//class


}
