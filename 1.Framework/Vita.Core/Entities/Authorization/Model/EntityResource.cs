using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Model.Construction;

namespace Vita.Entities.Authorization {

  /// <summary>A resource representing an entity and optionally a subset of entity properties that are subject to authorization check under this resource.</summary>
  public class EntityResource {
    public Type EntityType;
    /// <summary>Comma-delimited list of prop groups or properties identifying properties included in the resource.</summary>
    public string Properties; 
    //MemberMask is set by Init method
    public EntityMemberMask MemberMask;

    public EntityResource(Type entityType, string properties = null) {
      EntityType = entityType;
      Properties = properties; 
    }

    private bool _initialized; //a flag to avoid multiple initialization

    public void Init(EntityModel model) {
      if (_initialized) return;
      _initialized = true;
      var entInfo = model.GetEntityInfo(EntityType);
      Util.Check(entInfo != null, "Entity {0} is not part of entity model.", EntityType);
      if (!string.IsNullOrWhiteSpace(Properties)) 
        MemberMask = EntityMemberMask.Create(entInfo, Properties);
    }

    public override string ToString() {
      var str = EntityType.Name;
      if (!string.IsNullOrWhiteSpace(Properties))
        str += ":" + Properties;
      return str;
    }

  }//class

}
