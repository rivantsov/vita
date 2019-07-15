using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.MetaD1 {
  public static class Md1Extensions {

    // book -> book + publisher
    public static JoinPart AddJoin(this EntityView bkView, EntityInfo pubEnt, JoinPartMember bkToPubMember) {
      var isNullable = bkToPubMember.SourceMember.Flags.IsSet(EntityMemberFlags.Nullable);
      var joinType = isNullable ? TableJoinType.LeftOuter : TableJoinType.Inner;
      var part = new JoinPart() { View = bkView, Entity = pubEnt, JoinType = joinType};
      var link = new JoinPartLink() { OtherMember = bkToPubMember };
      part.JoinLinks.Add(link);
      bkView.Joins.Add(part); 
      return part; 
    }

    public static JoinPart GetRoot(this EntityView view) {
      return view.Joins[0]; 
    }

    public static JoinPartMember GetCreateMember(this JoinPart part, string memberName) {
      var member = part.Members.FirstOrDefault(m => m.Name == memberName);
      if (member != null)
        return member;
      var entMember = part.Entity.Members.FirstOrDefault(m => m.MemberName == memberName);
      Util.Check(entMember != null, "Member {0} not found on entity {1}", memberName, part.Entity.Name);
      member = new JoinPartMember() { JoinPart = part, Name = memberName, SourceMember = entMember };
      part.Members.Add(member);
      return member; 
    }

    public static List<JoinPartMember> GetMembers(this JoinPart part, params string[] names) {
      var list = new List<JoinPartMember>();
      foreach (var name in names)
        list.Add(part.GetCreateMember(name));
      return list; 
    }

    public static object ExecuteViewQuery(this IEntitySession session, ViewQuery query) {
      var lambda = BuildViewQueryLambda(session, query);
      var linqCmd = new Md1LinqCommand(session, query, lambda: null);
      var entSession = (EntitySession)session;
      var result = entSession.ExecuteLinqCommand(linqCmd);

      return result;
    }

    // ------------------ Building lambda -----------------------------------
    public static LambdaExpression BuildViewQueryLambda(IEntitySession session, ViewQuery query) {
      // var model = session.Context.App.Model;
      var view = query.View;
      var entSet0 = view.GetRoot().Entity.EntitySetConstant;

      var currExpr = entSet0;
      for(int i = 1; i < view.Joins.Count; i++) {
        var part = view.Joins[i];
        var entSet = part.Entity.EntitySetConstant;
        // For now handling only joins using refs, like bk => pub,  
        var lnkMember = part.JoinLinks[0].OtherMember;
        Util.Check(lnkMember != null, "Invlid join link, OtherMember may not be null"); 
        var joinExpr = Queryable.Join(currExpr, entSet, ;
      }
      

    }
  }


}
