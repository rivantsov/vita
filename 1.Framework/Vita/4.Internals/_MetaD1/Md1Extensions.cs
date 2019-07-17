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
  using System.Data;
  using Vita.Data.Linq.Translation;
  using Vita.Entities.Utilities;

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
      var paramList = new List<ParameterExpression>();
      var select = BuildSelect(session, query, paramList);
      var linqCmd = new Md1LinqCommand(session, query, select);
      linqCmd.Lambda = Expression.Lambda(select, paramList.ToArray());
      linqCmd.Options |= QueryOptions.NoQueryCache;
      // assign param values
      foreach(var prm in paramList) {

      }
      var entSession = (EntitySession)session;
      var result = entSession.ExecuteLinqCommand(linqCmd);
      return result;
    }

    // ------------------ Building SelectExpr -----------------------------------
    public static SelectExpression BuildSelect(IEntitySession session, ViewQuery query, List<ParameterExpression> paramList) {
      var ds = session.Context.App.DataAccess.GetDataSource(session.Context);
      var dbModel = ds.Database.DbModel;
      var select = new SelectExpression(null);
      // var model = session.Context.App.Model;
      var view = query.View;
      var tblLkp = new Dictionary<JoinPart, TableExpression>();
      foreach(var joinEnt in view.Joins) {
        var tblInfo = dbModel.GetTable(joinEnt.Entity);
        var tblExpr = new TableExpression(tblInfo);
        tblExpr.Alias = tblInfo.DefaultSqlAlias + "$";
        select.Tables.Add(tblExpr);
        tblLkp[joinEnt] = tblExpr; 
      }

      if (tblLkp.Count > 1)
        CheckTableAliases(tblLkp.Values.ToList()); 
      // setup joins 
      foreach(var de in tblLkp) {
        var part = de.Key;
        if (part.JoinLinks.Count == 0)
          continue;
        var lnkMember = part.JoinLinks[0].OtherMember;
        Util.Check(lnkMember != null, "Invalid join link, OtherMember may not be null");
        var baseTbl = tblLkp[lnkMember.JoinPart];
        var joinedTbl = de.Value;

        // build joinExpr
        var joinType = lnkMember.SourceMember.Flags.IsSet(EntityMemberFlags.Nullable) ? TableJoinType.LeftOuter : TableJoinType.Inner;
        Expression joinExpr = null;
        var fromMembers = lnkMember.SourceMember.ReferenceInfo.FromKey.ExpandedKeyMembers.Select(km => km.Member).ToList();
        var toCols = joinedTbl.TableInfo.PrimaryKey.KeyColumns.Select(kc => kc.Column).ToList();
        for(int i = 0; i < fromMembers.Count; i++) {
          var fkMember = fromMembers[i];
          var pkCol = toCols[i]; 
          var fkColExpr = FindCreateColumn(select, baseTbl, fkMember);
          var pkColExpr = FindCreateColumn(select, joinedTbl, pkCol);
          var eqExpr = Expression.Equal(fkColExpr, pkColExpr);
          joinExpr = joinExpr == null ? eqExpr : Expression.And(joinExpr, eqExpr);
        }
        // It should be in this way (joinedTbl.Join...) due to some internal mechanics in Linq translator
        joinedTbl.Join(joinType, baseTbl, joinExpr);
      } //foreach de

      // Limit/offset
      if (query.Skip > 0 || query.Take > 0) {
        select.Offset = Expression.Constant(query.Skip);
        select.Limit = Expression.Constant(query.Take);
      }

      // order by
      var orderBy = query.OrderBy.Count > 0 ? query.OrderBy : query.View.DefaultOrderBy;
      if (orderBy.Count > 0) {
        foreach(var spec in orderBy) {
          var part = spec.Member.JoinPart;
          var tbl = tblLkp[part];
          var col = FindCreateColumn(select, tbl, spec.Member.SourceMember);
          select.OrderBy.Add(new OrderByExpression(spec.Desc, col));
        }
      }
      // output columns
      foreach(var outMember in query.OutMembers) {
        var tbl = tblLkp[outMember.JoinPart];
        var outCol = FindCreateColumn(select, tbl, outMember.SourceMember);
        select.Operands.Add(outCol); 
      }

      // Filters
      foreach(var filter in query.Filters) {
        var tbl = tblLkp[filter.Member.JoinPart];
        var col = FindCreateColumn(select, tbl, filter.Member.SourceMember);
        var prm = Expression.Parameter(filter.Member.SourceMember.DataType, "@" + filter.Member.Name);
        paramList.Add(prm);
        var eqExpr = Expression.Equal(col, prm);
        select.Where.Add(eqExpr); 
        
      }

      // RowReader
      select.RowReaderLambda = ToLambda((rec, s) => ReadViewRow(rec, s, query));

      return select; 
    }

    private static void CheckTableAliases(IList<TableExpression> tables) {
      var aliases = new StringSet(); 
      foreach(var tbl in tables) {
        if (aliases.Contains(tbl.Alias)) {
          var baseAlias = tbl.Alias;
          int index = 1;
          do {
            tbl.Alias = baseAlias + (index++);
          }
          while (aliases.Contains(tbl.Alias));
        } //if
        aliases.Add(tbl.Alias);
      } //foreach tbl
    }

    private static ColumnExpression FindCreateColumn(SelectExpression select, TableExpression table, EntityMemberInfo member) {
      var col = select.Columns.FirstOrDefault(c => c.Table == table && c.ColumnInfo.Member == member);
      if (col != null)
        return col;
      var colInfo = table.TableInfo.Columns.First(c => c.Member == member);
      col = new ColumnExpression(table, colInfo);
      select.Columns.Add(col);
      return col; 
    }
    private static ColumnExpression FindCreateColumn(SelectExpression select, TableExpression table, DbColumnInfo colInfo) {
      var col = select.Columns.FirstOrDefault(c => c.Table == table && c.ColumnInfo == colInfo);
      if (col != null)
        return col;
      col = new ColumnExpression(table, colInfo);
      select.Columns.Add(col);
      return col;
    }

    private static DbTableInfo GetTable(this DbModel dbModel, EntityInfo entity) {
      return dbModel.Tables.First(t => t.Entity == entity);
    }

    private static LambdaExpression ToLambda(Expression<Func<IDataRecord, EntitySession, object>> func) {
      return func; 
    }

    private static object ReadViewRow(IDataRecord rec, EntitySession session, ViewQuery query) {
      var row = new ViewDataRow() { Members = query.OutMembers };
      var count = row.Members.Count; 
      row.Data = new object[count];
      for (int i = 0; i < count; i++)
        row.Data[i] = rec[i];
      return row; 
    }
  }

}
