using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization {

  /// <summary>Represents a set of permissions logically combined to allow performing a set of operations over data documents. </summary>
  public class Activity : HashedObject {
    public string Name;
    public List<Permission> Permissions = new List<Permission>();

    public List<Activity> ChildActivities = new List<Activity>(); 

    //Runtime data
    internal HashSet<Activity> AllChildActivities = new HashSet<Activity>();
    internal List<Permission> AllPermissions = new List<Permission>();

    public Activity(string name, params Permission[] permissions) : this(name) {
      if (permissions != null)
        Permissions.AddRange(permissions);
    }
    public Activity(string name, params Activity[] childActivities) : this(name) {
      AddActivities(childActivities);
    }
    private Activity(string name) {
      Name = name;
    }
    public void AddPermissions(params Permission[] children) {
      if (children == null) return; 
      Util.CheckAllNotNull(children, "Activity.AddPermissions: child permission is null. Activity={0}", this.Name);
      Permissions.AddRange(children); 
    }

    public void AddActivities(params Activity[] children) {
      if (children == null) return;
      Util.CheckAllNotNull(children, "Activity.AddActivities: child activity is null. Activity={0}", this.Name);
      ChildActivities.AddRange(children);
    }
    public override string ToString() {
      return Name ?? "(Unnamed)";
    }
    bool _initialized; //to avoid multiple initializations
    public void Init(EntityModel model) {
      if (_initialized)
        return;
      _initialized = true;
      // child activities
      AllChildActivities.UnionWith(ChildActivities); 
      AllPermissions.AddRange(this.Permissions); 
      foreach (var child in ChildActivities) {
        child.Init(model);
        AllChildActivities.UnionWith(child.AllChildActivities); 
        AllPermissions.AddRange(child.AllPermissions); 
      }
      //Permissions
      foreach (var perm in this.Permissions) {
        var entPerm = perm as EntityGroupPermission;
        if (entPerm != null) 
          entPerm.Init(model);
      }
      Util.Check(!AllChildActivities.Contains(this), "Activity {0} is a child of itself, looping not allowed.", this.Name);
    }

  }//class
}
