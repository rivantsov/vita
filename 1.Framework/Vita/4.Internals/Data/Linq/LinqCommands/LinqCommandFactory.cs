using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Data.Sql;
using Vita.Entities.Runtime;
using System.Collections;

namespace Vita.Data.Linq {

  public class LinkTuple {
    public object LinkEntity { get; set; }
    public object TargetEntity { get; set; } 
  }

  public static class LinqCommandFactory {
    // for special linq commands: to avoid regenerating sql cache key we 'cache' it inside Key or ListInfo meta objects

    public static SpecialLinqCommand CreateSelectByPrimaryKey(EntitySession session, EntityKeyInfo key, LockType lockType, object[] keyValues) {
      // get sql cache key - we cache it inside KeyInfo only for no-lock queries
      string sqlCacheKey;
      if (lockType == LockType.None) 
        sqlCacheKey = key.SqlCacheKey_SelectByPkNoLock = key.SqlCacheKey_SelectByPkNoLock ??
          SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.SelectByKey, key.Entity.Name, key.Name, lockType, null);
      else
        sqlCacheKey = SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.SelectByKey, key.Entity.Name, key.Name, lockType, null);
      // build the command
      return new SpecialLinqCommand(session, sqlCacheKey, SpecialCommandSubType.SelectByKey, key, lockType, null, 
                                         keyValues, Setup_SelectByKey);
    }

    public static SpecialLinqCommand CreateSelectByKeyValueArray(EntitySession session, EntityKeyInfo key, 
                                     List<EntityKeyMemberInfo> orderBy, IList keyValues) {
      var sqlCacheKey = SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.SelectByKeyArray, 
                   key.Entity.Name, key.Name, LockType.None, orderBy);
      // build the command
      var paramValues = new object[] { keyValues };
      return new SpecialLinqCommand(session, sqlCacheKey, SpecialCommandSubType.SelectByKeyArray, key, LockType.None, orderBy,
                                         paramValues, Setup_SelectByKeyValueArray);
    }

    public static SpecialLinqCommand CreateCheckAnyChildRecords(EntityKeyInfo childKey, EntityRecord record) {
      childKey.SqlCacheKey_ChildExists = childKey.SqlCacheKey_ChildExists ??
        SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.ExistsByKey, childKey.Entity.Name,
          childKey.Name, LockType.None, null);
      return new SpecialLinqCommand(record.Session, childKey.SqlCacheKey_ChildExists, SpecialCommandSubType.ExistsByKey, childKey,
                    LockType.None, null, record.PrimaryKey.Values, Setup_CheckChildExists);
    }

    public static SpecialLinqCommand CreateSelectByKeyForListPropertyManyToOne(EntitySession session, ChildEntityListInfo listInfo, object[] keyValues) {
      Util.Check(listInfo.RelationType == EntityRelationType.ManyToOne, "Fatal: expected many-to-one list.");
      var fromKey = listInfo.ParentRefMember.ReferenceInfo.FromKey;
      listInfo.SqlCacheKey_SelectChildRecs = listInfo.SqlCacheKey_SelectChildRecs ??
        SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.SelectByKey, fromKey.Entity.Name, fromKey.Name, LockType.None, null);
      // it is simply select-by-key command
      return new SpecialLinqCommand(session, listInfo.SqlCacheKey_SelectChildRecs, SpecialCommandSubType.SelectByKey, fromKey, LockType.None,
                                     listInfo.OrderBy, keyValues, Setup_SelectByKey);
    }

    public static SpecialLinqCommand CreateSelectByKeyForListPropertyManyToMany(EntitySession session, 
                        ChildEntityListInfo listInfo, object[] keyValues) {
      Util.Check(listInfo.RelationType == EntityRelationType.ManyToMany, "Fatal: expected many-to-many list.");
      var fromKey = listInfo.ParentRefMember.ReferenceInfo.FromKey;
      listInfo.SqlCacheKey_SelectChildRecs = listInfo.SqlCacheKey_SelectChildRecs ??
        SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.SelectByKey, fromKey.Entity.Name, fromKey.Name, LockType.None, null);
      return new SpecialLinqCommand(session, listInfo.SqlCacheKey_SelectChildRecs, SpecialCommandSubType.ListManyToMany,
                                    listInfo, listInfo.OrderBy, keyValues, Setup_SelectManyToMany);
    }

    public static SpecialLinqCommand CreateSelectByKeyArrayForListPropertyManyToOne(EntitySession session, 
                                                                ChildEntityListInfo listInfo, IList keyValues) {
      Util.Check(listInfo.RelationType == EntityRelationType.ManyToOne, "Fatal: expected many-to-one list.");
      var fromKey = listInfo.ParentRefMember.ReferenceInfo.FromKey;
      listInfo.SqlCacheKey_SelectChildRecsForInclude = listInfo.SqlCacheKey_SelectChildRecsForInclude ??
        SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.SelectByKeyArray, fromKey.Entity.Name, fromKey.Name, LockType.None, null);
      // it is simply select-by-key-array command
      var orderBy = fromKey.ExpandedKeyMembers.Merge(listInfo.OrderBy);
      var paramValues = new object[] { keyValues };
      return new SpecialLinqCommand(session, listInfo.SqlCacheKey_SelectChildRecsForInclude, SpecialCommandSubType.SelectByKeyArray, 
                                     fromKey, LockType.None, orderBy, paramValues, Setup_SelectByKeyValueArray);
    }

    public static SpecialLinqCommand CreateSelectByKeyArrayForListPropertyManyToMany(EntitySession session,
           ChildEntityListInfo listInfo, IList keyValues) {
      Util.Check(listInfo.RelationType == EntityRelationType.ManyToMany, "Fatal: expected many-to-many list.");
      var fromKey = listInfo.ParentRefMember.ReferenceInfo.FromKey;
      listInfo.SqlCacheKey_SelectChildRecsForInclude = listInfo.SqlCacheKey_SelectChildRecsForInclude ??
        SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.ListManyToManyByArray, fromKey.Entity.Name, fromKey.Name, LockType.None, null);
      // it is simply select-by-key-array command
      var orderBy = fromKey.ExpandedKeyMembers.Merge(listInfo.OrderBy);
      var paramValues = new object[] { keyValues };
      return new SpecialLinqCommand(session, listInfo.SqlCacheKey_SelectChildRecsForInclude, SpecialCommandSubType.ListManyToManyByArray,
                                     listInfo, orderBy, paramValues, Setup_SelectByValueArrayManyToMany);
    }


    // Dynamic LINQ commands 
    public static DynamicLinqCommand CreateLinqSelect(EntitySession session, Expression queryExpression) {
      return new DynamicLinqCommand(session, queryExpression, LinqCommandKind.Dynamic, LinqOperation.Select);
    }

    public static DynamicLinqCommand CreateLinqNonQuery(EntitySession session, Expression queryExpression, 
                                                 LinqOperation op, EntityInfo updateEntity) {
      return new DynamicLinqCommand(session, queryExpression, LinqCommandKind.Dynamic, op, updateEntity);
    }


    // Setup methods 

    private static void Setup_SelectByKey(SpecialLinqCommand command) {
      var key = command.Key;
      var lockType = command.LockType;
      var orderBy = command.OrderBy; 

      var entType = key.Entity.EntityType;
      var prms = key.ExpandedKeyMembers.Select(m => Expression.Parameter(m.Member.DataType, "@" + m.Member.MemberName)).ToArray();
      var pred = ExpressionMaker.MakeKeyPredicate(key, prms);
      var entSet = (lockType == LockType.None) ? key.Entity.EntitySetConstant
                                               : ExpressionMaker.MakeEntitySetConstant(entType, lockType);
      var entSetFiltered = ExpressionMaker.MakeCallWhere(entSet, pred);
      var entSetOrdered = ExpressionMaker.AddOrderBy(entSetFiltered, command.OrderBy);
      var lambdaExpr = Expression.Lambda(entSetOrdered, prms);
      command.Lambda = lambdaExpr;
    }

    public static void Setup_CheckChildExists(SpecialLinqCommand command) {
      var key = command.Key;
      var entType = key.Entity.EntityType;
      //build expression
      var prms = key.ExpandedKeyMembers.Select(m => Expression.Parameter(m.Member.DataType, "@" + m.Member.MemberName)).ToArray();
      var entSet = key.Entity.EntitySetConstant;
      var genAny = LinqExpressionHelper.QueryableAny2ArgMethod.MakeGenericMethod(entType);
      var pred = ExpressionMaker.MakeKeyPredicate(key, prms);
      var anyCallExpr = Expression.Call(genAny, entSet, pred);
      var lambdaExpr = Expression.Lambda(anyCallExpr, prms);
      command.Lambda = lambdaExpr;

    }

    private static void Setup_SelectByKeyValueArray(SpecialLinqCommand command) {
      var member = command.Key.ExpandedKeyMembers[0].Member; 
      var listType = typeof(IList<>).MakeGenericType(member.DataType);
      var listPrm = Expression.Parameter(listType, "@list");
      var entType = command.Key.Entity.EntityType;
      var pred = ExpressionMaker.MakeListContainsPredicate(member, listPrm);
      var entSet = member.Entity.EntitySetConstant;
      var entSetFiltered = ExpressionMaker.MakeCallWhere(entSet, pred);
      var entSetOrdered = ExpressionMaker.AddOrderBy(entSetFiltered, command.OrderBy);
      command.Lambda = Expression.Lambda(entSetOrdered, listPrm);
    }

    private static void Setup_SelectManyToMany(SpecialLinqCommand command) {
      /* Building smth like that: 
      var tuplesQuery = session.EntitySet<IBookAuthor>().Where(ba => ba.Book.Id == csBookId)
          .OrderBy(ba => ba.Author.LastName)
          .Select(ba => new Data.Linq.LinkTuple() { LinkEntity = ba, TargetEntity = ba.Author });
      
      the query returns list of LinkTuple objects <LinkEnt, TargetEnt>       

      */
      var listInfo = command.ListInfoManyToMany;
      var keyParentRef = listInfo.ParentRefMember.ReferenceInfo.FromKey;
      var prms = keyParentRef.ExpandedKeyMembers.Select(m => Expression.Parameter(m.Member.DataType, "@" + m.Member.MemberName)).ToArray();
      var wherePred = ExpressionMaker.MakeKeyPredicate(keyParentRef, prms);
      var linkEntSet = listInfo.LinkEntity.EntitySetConstant;
      var entSetFiltered = ExpressionMaker.MakeCallWhere(linkEntSet, wherePred);
      var targetRefMember = listInfo.OtherEntityRefMember;
      var entSetOrdered = AddOrderByManyToMany(entSetFiltered, listInfo.LinkEntity, listInfo.OtherEntityRefMember, command.OrderBy);
      var newTupleLambda = ExpressionMaker.MakeNewLinkTupleLambda(listInfo.OtherEntityRefMember);
      var selectTuple = ExpressionMaker.MakeCallSelect(entSetOrdered, newTupleLambda);
      command.Lambda = Expression.Lambda(selectTuple, prms);
    }

    private static void Setup_SelectByValueArrayManyToMany(SpecialLinqCommand command) {
      var listInfo = command.ListInfoManyToMany;
      var fkMember = listInfo.ParentRefMember.ReferenceInfo.FromKey.ExpandedKeyMembers[0].Member;
      var listType = typeof(IList<>).MakeGenericType(fkMember.DataType);
      var listPrm = Expression.Parameter(listType, "@list");
      var entType = listInfo.LinkEntity.EntityType;
      var wherePred = ExpressionMaker.MakeListContainsPredicate(fkMember, listPrm);
      var linkEntSet = fkMember.Entity.EntitySetConstant;
      var entSetFiltered = ExpressionMaker.MakeCallWhere(linkEntSet, wherePred);
      var targetRefMember = listInfo.OtherEntityRefMember;
      var entSetOrdered = AddOrderByManyToMany(entSetFiltered, listInfo.LinkEntity, listInfo.OtherEntityRefMember, command.OrderBy);
      var newTupleLambda = ExpressionMaker.MakeNewLinkTupleLambda(listInfo.OtherEntityRefMember);
      var selectTuple = ExpressionMaker.MakeCallSelect(entSetOrdered, newTupleLambda);
      command.Lambda = Expression.Lambda(selectTuple, listPrm);
    }

    public static List<EntityKeyMemberInfo> Merge(this List<EntityKeyMemberInfo> x, List<EntityKeyMemberInfo> y) {
      if(x == null || x.Count == 0)
        return y;
      if(y == null || y.Count == 0)
        return x;
      var merged = new List<EntityKeyMemberInfo>();
      merged.AddRange(x);
      merged.AddRange(y);
      return merged;
    }


    public static Expression AddOrderByManyToMany(Expression linkEntSet, EntityInfo linkEnt, EntityMemberInfo targetProp, 
                                                  List<EntityKeyMemberInfo> orderBy) {
      if(orderBy == null || orderBy.Count == 0)
        return linkEntSet;
      var entSetOrdered = linkEntSet;
      foreach(var km in orderBy) {
        if (km.Member.Entity == linkEnt) 
          entSetOrdered = ExpressionMaker.AddOrderBy(entSetOrdered, km.Member, km.Desc);
        else 
          entSetOrdered = ExpressionMaker.AddOrderBy(entSetOrdered, km.Member, km.Desc, targetProp);
      }
      return entSetOrdered;
    }



  }
}
