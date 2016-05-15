using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Common;
using Vita.Entities.Linq;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization {

  /// <summary>Represents a grant (enablement) of an activity, as part of a Role, with optional data filter for documents.</summary>
  public class ActivityGrant : HashedObject, IEquatable<ActivityGrant> {
    public Activity Activity;
    public AuthorizationFilter Filter; //might be null
    public bool IsDynamic;

    public ActivityGrant(Activity activity, AuthorizationFilter dataFilter = null) {
      Activity = activity;
      Filter = dataFilter;
      IsDynamic = this is DynamicActivityGrant;
    }

    //To suppress warning: if override Equals, then override GetHashCode
    public override int GetHashCode() {
      return base.GetHashCode();
    }

    public override bool Equals(object obj) {
      var actObj = obj as ActivityGrant;
      if (actObj == null) 
        return false; 
      return Equals(actObj);
    }
    public override string ToString() {
      var result = Activity.Name;
      if (Filter != null) 
        result += "/" + Filter;
      return result; 
    }

    #region IEquatable<ActivityGrant> Members
    public bool Equals(ActivityGrant other) {
      return this.Activity == other.Activity && this.Filter == other.Filter;
    }
    #endregion

    //Used at runtime
    public virtual ActivityGrant CreateSimilarGrant(Activity activity) {
      return new ActivityGrant(activity, Filter);
    }
  }


}
