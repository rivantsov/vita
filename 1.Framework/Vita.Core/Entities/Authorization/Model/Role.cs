using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Authorization.Runtime;
using Vita.Entities.Linq;

namespace Vita.Entities.Authorization {

  /// <summary> A container for combined permission sets assembled to match some organizational role. </summary>
  public class Role : HashedObject {
    public readonly int Id;
    public string Name;
    public List<ActivityGrant> ActivityGrants = new List<ActivityGrant>();
    public List<Role> ChildRoles = new List<Role>();

    internal RuntimeRoleData RuntimeData;
    private static int _idCounter;

    #region constructors
    public Role(string name) {
      Util.Check(!string.IsNullOrWhiteSpace(name), "Role name may not be empty.");
      Name = name;
      Id = _idCounter++;
    }

    public Role(string name, Role childRole, params Role[] moreRoles) : this(name) {
      ChildRoles.Add(childRole);
      if(moreRoles != null)
        ChildRoles.AddRange(moreRoles); 
    }

    public Role(string name, Activity activity, params Activity[] moreActivities) : this(name) {
      Grant(activity); 
      if (moreActivities != null)
        Grant(moreActivities);
    }

    #endregion

    public void Grant(params Activity[] activities) {
      Grant(null, activities);
    }

    public void Grant(AuthorizationFilter filter, params Activity[] activities) {
      foreach (var act in activities)
        ActivityGrants.Add(new ActivityGrant(act, filter));
    }

    public void Grant(params Permission[] permissions) { 
      Grant(null, permissions); 
    }
    public void Grant(AuthorizationFilter filter, params Permission[] permissions) { 
      Util.Check(permissions != null && permissions.Length > 0, "Role.Grant: Permission list is empty");
      // Create an activity
      var name = permissions[0].Name;
      if (permissions.Length > 1)
        name += "**";
      var act = new Activity(name, permissions); 
      Grant(filter, act);
    }

    public DynamicActivityGrant GrantDynamic(AuthorizationFilter filter, Activity activity,
         string documentKey = null, AuthorizationFilter documentFilter = null) {
      var grant = new DynamicActivityGrant(activity, filter, documentKey, documentFilter);
      ActivityGrants.Add(grant);
      return grant; 
    }
    
    bool _initialized; //to avoid multiple initializations
    public void Init(EntityModel model) {
      if (_initialized)
        return;
      _initialized = true;
      foreach (var role in this.ChildRoles)
        role.Init(model);
      foreach (var grant in this.ActivityGrants)
        grant.Activity.Init(model); 
    }

    public override string ToString() {
      return Name; 
    }


  }//class



}
