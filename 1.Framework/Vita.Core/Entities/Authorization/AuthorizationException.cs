using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;

namespace Vita.Entities.Authorization {
  using Runtime;
  using Vita.Entities.Runtime; 

  public class AuthorizationException : Exception {
    public readonly AccessType AccessType;
    public readonly Type EntityType;
    public readonly bool IsRecordLevel;
    public readonly UserRecordPermission GrantedPermissions;
    public readonly string UserName;
    public readonly ReadAccessLevel RequireReadMode;
    public readonly DenyReadActionType CurrentDenyReadMode; 
    public readonly string UserRoles;
    public readonly string EnabledDynamicGrants;
    public readonly string PermissionSummary;
    public readonly string UserContextValues;

    public AuthorizationException(string message, Type entityType, AccessType accessType, bool isRecordLevel, 
         UserRecordPermission grantedPermissions, SecureSession session = null) : base(message) {
      EntityType = entityType;
      AccessType = accessType;
      IsRecordLevel = isRecordLevel;
      GrantedPermissions = grantedPermissions;
      if (session != null) {
        RequireReadMode = session.DemandReadAccessLevel;
        CurrentDenyReadMode = session.DenyReadAction;
        var ctx = session.Context; 
        UserName = ctx.User.UserName;
        UserContextValues = string.Join(string.Empty, ctx.Values.Select(kv => StringHelper.SafeFormat("      [{0}]={1}\r\n", kv.Key, kv.Value)));
        var user = ctx.User;
        if (user.Authority == null) {
          UserRoles = "(UserContext.Authority is not set)";
        } else {
          UserRoles = user.Authority.GetRoleNames();
          PermissionSummary = user.Authority.GetPermissionsSummary(EntityType);
          var enDynGrants = user.Authority.DynamicGrants.Where(g => g.IsEnabled(session.Context));
          EnabledDynamicGrants = string.Join(",", enDynGrants.Select(g => g.Activity.Name));
        }
      }
    }

    public string Summary {
      get {
        return StringHelper.SafeFormat(
@"
    Message:             {0}
    EntityType:          {1} 
    AttemptedAction:     {2} 
    AtRecordLevel?       {3} 
    UserIdentity:        {4} 
    UserRoles:           {5}
    RequireReadAccess:   {6} 
    CurrentDenyReadMode: {7}
    Enabled dynamic grants: {8} 
    UserContext.Values: 
{9}
Granted permissions for record: 
  {10}
========= Permissions summary for entity {1}:
  {11}.",
            Message, EntityType, AccessType, IsRecordLevel, 
            UserName, UserRoles, RequireReadMode, CurrentDenyReadMode,
            EnabledDynamicGrants, UserContextValues, GrantedPermissions, PermissionSummary);
      }
    }
    public override string ToString() {
      return base.ToString() + "\r\n Authorization Information: " + Summary;
    }
  }
}
