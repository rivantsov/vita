using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization {

  /// <summary>A permission set for an entity group. Identifies resources (entities) and allowed actions. </summary>
  public class EntityGroupPermission : Permission {
    public AccessType AccessType; 
    public List<EntityGroupResource> GroupResources = new List<EntityGroupResource>();

    public EntityGroupPermission(string name, AccessType accessType, params EntityGroupResource[] groupResources) 
      : base(name) {
      AccessType = accessType;
      if (groupResources != null)
        Add(groupResources);
    }

    public EntityGroupPermission(string name, AccessType accessType, params Type[] entityTypes) : base(name) {
      AccessType = accessType;
      var resource = new EntityGroupResource(name + "_resource", entityTypes);
      GroupResources.Add(resource); 
    }

    public void Add(params EntityGroupResource[] resources) {
      Util.Check(resources != null, "Parameter resources may not be null.");
      Util.CheckAllNotNull(resources, "One or more of resource objects is null.");
      GroupResources.AddRange(resources);

    }
    public override string ToString() {
      return AccessType + "/" + string.Join(",", GroupResources.Select(r=>r.Name));
    }

    bool _initialized; //to avoid multiple initializations
    public void Init(EntityModel model) {
      if (_initialized)
        return;
      _initialized = true;
      foreach (var res in this.GroupResources)
        res.Init(model);
    }

  }
}
