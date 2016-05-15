using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Common;
using Vita.Entities.Linq;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization.Runtime {

  internal class RuntimeRoleData {
    // the following lists are fully expanded, so they contain all grand/grand/../children
    public HashSet<Role> AllChildRoles = new HashSet<Role>();
    public HashSet<ActivityGrant> GrantedActivities = new HashSet<ActivityGrant>();
    public HashSet<GrantedPermission> GrantedPermissions = new HashSet<GrantedPermission>();
  }


  internal class GrantedPermission : HashedObject, IEquatable<GrantedPermission> {
    public Permission Permission;
    public ActivityGrant Grant;

    public GrantedPermission(Permission permission, ActivityGrant grant) {
      Permission = permission;
      Grant = grant;
    }
    //required if you override equals
    public override int GetHashCode() {
      return base.GetHashCode();
    }

    public override bool Equals(object obj) {
      return this.Equals(obj as GrantedPermission);
    }

    #region IEquatable<T> Members
    public bool Equals(GrantedPermission other) {
      if (other == null) 
        return false; 
      return Permission == other.Permission && Grant == other.Grant;
    }
    #endregion
  }//class


}
