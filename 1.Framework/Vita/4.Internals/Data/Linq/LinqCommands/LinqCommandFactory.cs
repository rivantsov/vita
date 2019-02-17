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

namespace Vita.Data.Linq {

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

    public static SpecialLinqCommand CreateSelectByKeyForListProperty(EntitySession session, ChildEntityListInfo listInfo, object[] keyValues) {
      var fromKey = listInfo.ParentRefMember.ReferenceInfo.FromKey;
      listInfo.SqlCacheKey_SelectChildRecs = listInfo.SqlCacheKey_SelectChildRecs ??
        SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.SelectByKey, fromKey.Entity.Name, fromKey.Name, LockType.None, null);
      return new SpecialLinqCommand(session, listInfo.SqlCacheKey_SelectChildRecs, SpecialCommandSubType.SelectByKey, fromKey, LockType.None,
                                     listInfo.OrderBy, keyValues, Setup_SelectByKey);
    }

    public static SpecialLinqCommand CreateCheckAnyChildRecords(EntityKeyInfo childKey, EntityRecord record) {
      childKey.SqlCacheKey_ChildExists = childKey.SqlCacheKey_ChildExists ??
        SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.ExistsByKey, childKey.Entity.Name, 
          childKey.Name, LockType.None, null);
      return new SpecialLinqCommand(record.Session, childKey.SqlCacheKey_ChildExists,  SpecialCommandSubType.ExistsByKey, childKey, 
                    LockType.None, null, record.PrimaryKey.Values, Setup_CheckChildExists);
    }

    public static SpecialLinqCommand CreateSelectByKeyValueArray(EntitySession session, EntityKeyInfo key, 
                                     List<EntityKeyMemberInfo> orderBy, System.Collections.IList keyValues) {
      var sqlCacheKey = SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialCommandSubType.SelectByKeyArray, 
                   key.Entity.Name, key.Name, LockType.None, orderBy);
      // build the command
      var paramValues = new object[] { keyValues };
      return new SpecialLinqCommand(session, sqlCacheKey, SpecialCommandSubType.SelectByKeyArray, key, LockType.None, orderBy,
                                         paramValues, Setup_SelectByKeyValueArray);
    }


    public static DynamicLinqCommand CreateLinqSelect(EntitySession session, Expression queryExpression) {
      return new DynamicLinqCommand(session, queryExpression, LinqCommandKind.Dynamic, LinqOperation.Select);
    }

    public static DynamicLinqCommand CreateLinqNonQuery(EntitySession session, Expression queryExpression, 
                                                 LinqOperation op, EntityInfo updateEntity) {
      return new DynamicLinqCommand(session, queryExpression, LinqCommandKind.Dynamic, op, updateEntity);
    }




    public static void Setup_SelectByKey(SpecialLinqCommand command) {
      var key = command.Key;
      var lockType = command.LockType;
      var orderBy = command.OrderBy; 

      var entType = key.Entity.EntityType;
      var prms = key.ExpandedKeyMembers.Select(m => Expression.Parameter(m.Member.DataType, "@" + m.Member.MemberName)).ToArray();
      var pred = ExpressionMaker.MakeKeyPredicate(key, prms);
      var entSet = (lockType == LockType.None) ? key.Entity.EntitySetConstant
                                               : ExpressionMaker.MakeEntitySetConstant(entType, lockType);
      var entSetFiltered = ExpressionMaker.MakeCallWhere(entSet, entType, pred);
      var entSetOrdered = AddOrderBy(entSetFiltered, entType, command.OrderBy);
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
      var pred = ExpressionMaker.MakeListContainsPredicate(entType, member, listPrm);
      var entSet = member.Entity.EntitySetConstant;
      var entSetFiltered = ExpressionMaker.MakeCallWhere(entSet, entType, pred);
      var entSetOrdered = AddOrderBy(entSetFiltered, entType, command.OrderBy);
      command.Lambda = Expression.Lambda(entSetOrdered, listPrm);
    }

    private static Expression AddOrderBy(Expression entSet, Type entityType, List<EntityKeyMemberInfo> orderBy) {
      if(orderBy == null || orderBy.Count == 0)
        return entSet;
      var entSetOrdered = entSet;
      foreach(var km in orderBy)
        entSetOrdered = ExpressionMaker.AddOrderBy(entSetOrdered, entityType, km.Member, km.Desc);
      return entSetOrdered;
    }


  }
}
