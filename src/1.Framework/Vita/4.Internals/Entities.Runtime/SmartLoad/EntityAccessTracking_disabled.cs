using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime.SmartLoad {
  // tracking which members are read

  public class EntityTrackingContext {
    public string ContextKey;
    public List<EntityReadTracker> EntityTrackers = new List<EntityReadTracker>(); 
  }

  public class EntityReadTracker {
    public EntityReadTracker Parent;
    public EntityMemberInfo ParentMember;
    public EntityInfo Entity;
    public List<EntityReadTracker> ChildTrackers = new List<EntityReadTracker>(); 
    public string ContextSubKey; // usually null

    public EntityMemberMask ReadMask;

    public void OnMemberRead(EntityMemberInfo member) {

    }
  }

}
