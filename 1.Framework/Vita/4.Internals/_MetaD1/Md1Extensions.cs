using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
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

    public static object ExecuteViewQuery(this IEntitySession session, ViewQuery query, DbModel dbModel) {
      var select = BuildSelect(session, query, dbModel);
      var linqCmd = new Md1LinqCommand(session, query, lambda: null);
      var entSession = (EntitySession)session;
      var result = entSession.ExecuteLinqCommand(linqCmd);

      return result;
    }

    // ------------------ Building lambda -----------------------------------
    public static LambdaExpression BuildSelect(IEntitySession session, ViewQuery query, DbModel dbModel) {
      var select = new SelectExpression(null);
      // var model = session.Context.App.Model;
      var view = query.View;
      var tblLkp = new Dictionary<JoinPart, TableExpression>();      
      for(int i = 0; i < view.Joins.Count; i++) {
        var part = view.Joins[i];
        var tblInfo = dbModel.GetTable(part.Entity);
        var tblExpr = new TableExpression(tblInfo);
        tblExpr.Alias = part.Alias;
        select.Tables.Add(tblExpr);
        tblLkp[part] = tblExpr; 
        if (i == 0)
          continue; 
        var lnkMember = part.JoinLinks[0].OtherMember;
        Util.Check(lnkMember != null, "Invlid join link, OtherMember may not be null");
        tblExpr.Join()
      }
      // setup joins 
      foreach(var de in tblLkp) {
        var part = de.Key;
        if (part.JoinLinks.Count == 0)
          continue;
        var lnkMember = part.JoinLinks[0].OtherMember;
        Util.Check(lnkMember != null, "Invlid join link, OtherMember may not be null");
        var fromTbl = tblLkp[lnkMember.JoinPart];
        var joinedTbl = de.Value;
        // build joinExpr
        var fromCol = FindCreateColumn(select, fromTbl, lnkMember.SourceMember);
        var toPkMember = joinedTbl.TableInfo.PrimaryKey.KeyColumns[0];
        var toCol = FindCreateColumn(select, joinedTbl, joinedTbl)
        fromTbl.Join(TableJoinType.Inner, joinedTbl, joinExpr, joinedTbl.Alias);
      }

    }

    private static ColumnExpression FindCreateColumn(SelectExpression select, TableExpression table, EntityMemberInfo member) {
      var col = select.Columns.FirstOrDefault(c => c.Table == table && c.ColumnInfo.Member == member);
      if (col != null)
        return col;
      var colInfo = table.TableInfo.GetColumnByMemberName(member.MemberName);
      col = new ColumnExpression(table, colInfo);
      select.Columns.Add(col);
      return col; 
    }

    private static DbTableInfo GetTable(this DbModel dbModel, EntityInfo entity) {
      return dbModel.Tables.First(t => t.Entity == entity);
    }
  }

}
