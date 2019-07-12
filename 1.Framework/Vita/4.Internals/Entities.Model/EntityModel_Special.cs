using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Linq.Translation.Expressions;

namespace Vita.Entities.Model.Special {

  // Note: for now, view is join of entities only, not views
  public class EntityView {
    public string Name; 
    public List<JoinPart> Joins = new List<JoinPart>();
    public List<ViewMember> GroupBy = new List<ViewMember>();

    public EntityView(EntityInfo entity) {
      var jEnt = new JoinPart() { Entity = entity };
      Joins.Add(jEnt);
      Name = "V_" + entity.Name;
    }
  }

  public class ViewMember {
    public string Name; 
    public EntityMemberInfo SourceMember;
    public JoinPart JoinPart;
    public AggregateType Aggregate;
    public string AggregateExpression;
    public string Expr;
  }

  public class JoinPart {
    public EntityInfo Entity;
    public TableJoinType JoinType;
    public EntityMemberInfo JoinMember;
    public string Alias;
  }




}
