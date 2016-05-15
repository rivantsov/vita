using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Runtime;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization.Runtime {

  //Static utility class
  internal class AuthorityBuilder {
    private static object _lock = new object();
    EntityApp _app;

    public AuthorityBuilder(EntityApp app) {
      _app = app; 
    }

    public Authority BuildAuthority(string authorityKey, IList<Role> roles) {
      lock(_lock) {
        // prepare roles
        foreach(var role in roles)
          if(role.RuntimeData == null)
            BuildRuntimeData(_app.Model, role);
        var authority = new Authority(authorityKey, roles);
        BuildAuthorityPermissions(authority);
        return authority; 
      }
    }

    public void BuildRuntimeData(EntityModel entityModel, Role role) {
      if(role.RuntimeData != null) return;
      role.Init(entityModel);
      role.RuntimeData = new RuntimeRoleData();
      // get refs to 'all' sets
      var allChildRoles = role.RuntimeData.AllChildRoles;
      var allChildGrants = role.RuntimeData.GrantedActivities;
      allChildGrants.UnionWith(role.ActivityGrants); //initialize with direct grants
      //Process child roles; collect roles and grants
      foreach(var child in role.ChildRoles)
        if(allChildRoles.Add(child)) {
          BuildRuntimeData(entityModel, child);
          allChildRoles.UnionWith(child.RuntimeData.AllChildRoles); //merge child roles
          allChildGrants.UnionWith(child.RuntimeData.GrantedActivities); //merge all child grants         
        }
      //check there's no circular parent/child relationships
      Util.Check(!allChildRoles.Contains(role),
        "Role {0} is an (indirect) child of itself.", role.Name);
      // Now we merged all activity grants from child roles into one flat set allChildRoles
      // All activity grants from this and all child roles are merged into  allChildGrants
      // What we need to add to allChildGrants is grand-child activities - child activities 
      // of direct grants for this role. If this role R has grant G on activity A, and activity
      // A has child activity B, then effectively role R is granted activity B through the same
      // grant G (or its clone). If grant G has conditions (filter, dynamic condition),
      // then activity B is granted to role R with the same conditions.
      // So we grant grand-child activities (B's) through clones of child grant G.
      foreach(var actGrant in role.ActivityGrants) {
        foreach(var grandChild in actGrant.Activity.AllChildActivities) {
          var grandChildGrand = actGrant.CreateSimilarGrant(grandChild);
          allChildGrants.Add(grandChildGrand);
        }
      }
      //Consolidate all permissions in one plane set of (grant/permission) tuples
      foreach(var grant in role.RuntimeData.GrantedActivities) {
        foreach(var perm in grant.Activity.Permissions) {
          role.RuntimeData.GrantedPermissions.Add(new GrantedPermission(perm, grant));
        }
      }
    }


    internal void BuildAuthorityPermissions(Authority authority) {
      var nonEntPermGrants = authority.NonEntityPermissionGrants;
      var objPermissions = authority.ObjectPermissionTable; 
      var dynGrants = authority.DynamicGrants;
      foreach (var role in authority.Roles) {
        foreach (var grantedPerm in role.RuntimeData.GrantedPermissions) {
          var dynGrant = grantedPerm.Grant as DynamicActivityGrant;
          if (dynGrant != null)
            dynGrants.Add(dynGrant);
          var entPerm = grantedPerm.Permission as EntityGroupPermission;
          if(entPerm != null) {
            AddEntityPermission(authority, grantedPerm.Grant, entPerm);
            continue; 
          }
          var objPerm = grantedPerm.Permission as ObjectAccessPermission;
          if(objPerm != null) {
            foreach(var objType in objPerm.Types) {
              AccessType newAccess = objPerm.AccessType;
              AccessType oldAccess;
              if(objPermissions.TryGetValue(objType, out oldAccess))
                newAccess |= oldAccess; 
              objPermissions[objType] = newAccess; 
            }//foreach objType
            continue; 
          }//if objPerm != null
          //Non-entity permission
          nonEntPermGrants.Add(grantedPerm);
        }// foreach permGrant

      } //foreach role
      // Final processing: complete processing for each entPermSet
      foreach(var entPermSet in authority.EntityPermissionTable.Values) 
        FinalizePermissionSet(entPermSet);
    }

    private void AddEntityPermission(Authority authority, ActivityGrant grant, EntityGroupPermission entPerm) {
      // Entity group permission. Go through each entity in groups
      var filter = grant.Filter;
      foreach(var entGroupRes in entPerm.GroupResources) {
        foreach(var entRes in entGroupRes.Entities) {
          var entType = entRes.EntityType;
          var newRecPerms = new UserRecordPermission(entPerm.AccessType, entRes.MemberMask);
          //Find/create entity permission set for the entity type
          UserEntityPermissionSet permSet = authority.GetEntityPermissionSet(entType, create: true);
          var log = "  Source permission " + entPerm.Name + ":";
          // Go through each permission and try to merge
          var compatiblePerm = permSet.ConditionalPermissions.FirstOrDefault(p => p.CanMerge(grant));
          if(compatiblePerm == null) {
            //create new cumulative permission
            var permId = "P" + permSet.ConditionalPermissions.Count; //artificial Id
            var newPerm = new CumulativeRecordPermission(permId, entType, newRecPerms, grant);
            permSet.ConditionalPermissions.Add(newPerm);
            log += " - added as " + permId;
          } else {
            //merge
            compatiblePerm.RecordPermission.Merge(newRecPerms);
            compatiblePerm.SourceGrants.Add(grant); //add grant
            log += " - merged into " + compatiblePerm.Id;
          }
          permSet.LogBuilder.AppendLine(log);
        } //foreach entRes
      }//foreach entGroupRes
    }

    private void FinalizePermissionSet(UserEntityPermissionSet permissionSet) {
      foreach (var perm in permissionSet.ConditionalPermissions) {
        //complete perm
        var dynGrants = perm.SourceGrants.Select(g => g as DynamicActivityGrant).Where(g => g != null).ToArray();
        perm.DynamicGrants = (dynGrants.Length == 0) ? null : dynGrants;
        permissionSet.HasFilter |= perm.HasFilter;
        permissionSet.HasDynamicPermissions |= perm.DynamicGrants != null;
        if (!permissionSet.HasFilter && !permissionSet.HasDynamicPermissions)
          permissionSet.FixedRecordPermissions.Merge(perm.RecordPermission);
        if (perm.DynamicGrants == null)
          permissionSet.FixedTypePermissions.Merge(perm.RecordPermission);
      }
      if (permissionSet.HasFilter) {
        permissionSet.FixedRecordPermissions = null; // FixedRights are there only if there are no record-level permissions
        permissionSet.FixedTypePermissions.HasFilter = true; // indicate that there are record-level restrictions
      }
      if (permissionSet.HasDynamicPermissions)
        permissionSet.FixedTypePermissions = null;
      // Query filter
      BuildLinqFilter(permissionSet);
      //finalize log
      permissionSet.Log = permissionSet.LogBuilder.ToString();
      permissionSet.LogBuilder = null; 
    }

    private void BuildLinqFilter(UserEntityPermissionSet permissionSet) {
      permissionSet.QueryFilterPredicate = null; 
      var readPerms = permissionSet.ConditionalPermissions.Where(
                           p => p.RecordPermission.AccessTypes.IsSet(AccessType.Peek | AccessType.Read));
      if(readPerms.Any(p => p.QueryPredicate == null)) // if any permission has no linq filter, it means all allowed through this permission
        return;
      var allFilters = readPerms.Where(p => p.QueryPredicate != null).Select(p => p.QueryPredicate).Distinct().ToList();
      if(allFilters.Count == 0)
        return;
      permissionSet.QueryFilterPredicate = QueryFilterHelper.CombinePredicatesWithOR(allFilters);
    }


  }//class
}//ns
