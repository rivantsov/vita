using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime.SmartLoad {

  public class SourceQuery {
    public int[] RecordIds;
    public EntityLoadPlan LoadPlan; 
  }

  public class EntityLoadPlan {
    public string ContextSubKey; // usually null
    public EntityMemberInfo ParentMember; 
    public EntityInfo Entity;
    public EntityMemberMask Members;
  }

  public class LoadPlan {
    public string ContextKey; // GraphQL query, or REST endpoint URL
    public List<EntityLoadPlan> EntityPlans = new List<EntityLoadPlan>();
  }

}


