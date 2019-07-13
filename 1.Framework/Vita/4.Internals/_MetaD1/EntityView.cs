using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Entities.Model;

namespace Vita.Entities.MetaD1 {

  // Note: for now, view is join of entities only, not views
  public class EntityView {
    public string Name; 
    public List<JoinPart> Joins = new List<JoinPart>();
    public List<JoinPartMember> GroupBy = new List<JoinPartMember>();

    public EntityView(EntityInfo entity) {
      var jEnt = new JoinPart() { Entity = entity };
      Joins.Add(jEnt);
      Name = "V_" + entity.Name;
    }
  }

  public class JoinPartMember {
    public string Name; 
    public EntityMemberInfo SourceMember;
    public JoinPart JoinPart;
  }

  public class AggregateMember: JoinPartMember {
    public AggregateType Aggregate;
    public string AggregateExpression;
    public string Expr;

  }

  public class JoinPart {
    public TableJoinType JoinType;
    public string Alias;
    // Either Entity or View is null
    public EntityInfo Entity; //for simple, one-entity part
    public EntityView View;  // for part which is View itself
    public List<JoinPartLink> JoinLinks;
  }

  public class JoinPartLink {
    public JoinPartMember ThisMember;
    public JoinPartMember OtherMember;
  }

}
