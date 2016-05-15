using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization.Runtime {

  // Container for combined record permissions; we combine permissions with the same filter (or no filter) 
  internal class CumulativeRecordPermission {
    public string Id; // artificial name (P1, P2..) to refer to permission in creation log
    public Type EntityType;
    public UserRecordPermission RecordPermission;
    public EntityPredicate FilterPredicate;
    public QueryPredicate QueryPredicate;
    internal HashSet<ActivityGrant> SourceGrants = new HashSet<ActivityGrant>();

    public bool HasFilter;
    //If there is at least one static grant, then this property is null
    public DynamicActivityGrant[] DynamicGrants;

    public CumulativeRecordPermission(string id, Type entityType, UserRecordPermission initialPermissions, ActivityGrant grant) {
      Id = id;
      EntityType = entityType;
      RecordPermission = initialPermissions;
      SourceGrants.Add(grant);
      if(grant.Filter != null) {
        FilterPredicate = grant.Filter.EntityFilter.GetPredicate(entityType);
        QueryPredicate = grant.Filter.QueryFilter.GetPredicate(entityType);
      }
      HasFilter = (FilterPredicate != null);
    }

    public bool CanMerge(ActivityGrant fromGrant) {
      if(this.SourceGrants.Contains(fromGrant))
        return true; //Grant is already there
      var otherFilter = fromGrant.Filter;
      FilterPredicate otherEntPred = (otherFilter == null) ? null : fromGrant.Filter.EntityFilter.GetPredicate(this.EntityType);
      FilterPredicate otherLinqPred = (otherFilter == null) ? null : fromGrant.Filter.QueryFilter.GetPredicate(this.EntityType);
      if(this.FilterPredicate != otherEntPred || this.QueryPredicate != otherLinqPred)
        return false; // if filters do not match, then false
      //Check dynamic grant compatibility. Only dynamic grants matter
      if(fromGrant is DynamicActivityGrant) {
        return this.DynamicGrants.Contains(fromGrant); // true if dynamic grant is already in permission's grant list
      }
      return true;
    }

    internal bool IsActive(OperationContext context) {
      if(DynamicGrants == null || DynamicGrants.Length == 0)
        return true; //if it has no dynamic grants, it is active
      for(int i = 0; i < DynamicGrants.Length; i++)
        if(DynamicGrants[i].IsEnabled(context)) return true;
      return false;
    }

    public override string ToString() {
      var result = StringHelper.SafeFormat("{0}: {1}/{2}", Id, EntityType, RecordPermission);
      if(FilterPredicate != null)
        result += "/Filter:" + FilterPredicate.ToString();
      if(DynamicGrants != null)
        result += "/Dynamic, activated by: " + string.Join(",", DynamicGrants.Select(g => g.Activity.Name));
      return result;
    }

  }//class

}
