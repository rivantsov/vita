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
    static IList<ParameterExpression> _emptyParamList = new ParameterExpression[] { };

    public static LinqCommand CreateSelectByKey(EntitySession session, EntityKeyInfo key, LockType lockType,
                   List<EntityKeyMemberInfo> orderBy, object[] keyValues) {
      return new SpecialLinqCommand(session,  SpecialCommandSubType.SelectByKey, key, lockType, orderBy, keyValues, Setup_SelectByKey);
    }


    public static LinqCommand CreateCheckAnyChildRecords(EntityKeyInfo childKey, EntityRecord record) {
      return new SpecialLinqCommand(record.Session, SpecialCommandSubType.ExistsByKey, childKey, LockType.None, null, 
              record.PrimaryKey.Values, Setup_CheckAnyChild);
    }

    public static LinqCommand CreateLinqSelect(EntitySession session, Expression queryExpression) {
      return new LinqCommand(session, queryExpression, LinqCommandKind.Dynamic, LinqOperation.Select, setup: Setup_DynamicLinqCommand);
    }

    public static LinqCommand CreateLinqNonQuery(EntitySession session, Expression queryExpression, 
                                                 LinqOperation op, EntityInfo updateEntity) {
      return new LinqCommand(session, queryExpression, LinqCommandKind.Dynamic, op, updateEntity, Setup_DynamicLinqCommand);
    }

    public static void Setup_DynamicLinqCommand(LinqCommand command) {
      LinqCommandRewriter.RewriteToLambda(command);
    }

    public static void Setup_SelectByKey(LinqCommand command) {
      var scmd = (SpecialLinqCommand)command;
      var key = scmd.Key;
      var lockType = scmd.LockType;
      var orderBy = scmd.OrderBy; 

      var entType = key.Entity.EntityType;
      var prms = key.ExpandedKeyMembers.Select(m => Expression.Parameter(m.Member.DataType, "@" + m.Member.MemberName)).ToArray();
      var pred = ExpressionMaker.MakeKeyPredicate(key, prms);
      var entSet = (lockType == LockType.None) ? key.Entity.EntitySetConstant
                                               : ExpressionMaker.MakeEntitySetConstant(entType, lockType);
      var entSetFiltered = ExpressionMaker.MakeCallWhere(entSet, entType, pred);
      var entSetOrdered = entSetFiltered;
      if(orderBy != null && orderBy.Count > 0)
        foreach(var km in orderBy)
          entSetOrdered = ExpressionMaker.AddOrderBy(entSetOrdered, entType, km.Member, km.Desc);
      var lambdaExpr = Expression.Lambda(entSetOrdered, prms);
      command.Lambda = lambdaExpr;
    }

    public static void Setup_CheckAnyChild(LinqCommand command) {

    }

    /*
    public static LinqCommand BuildSelectByKeyValueArray(EntityKeyInfo key, EntityMemberMask mask = null,
                                                          IList<EntityKeyMemberInfo> orderBy = null) {
      Util.Check(key.KeyMembers.Count == 1, "Fatal: cannot build IN-array query for composite keys. Key: {0}", key);
      var member = key.KeyMembers[0].Member;
      var entType = member.Entity.EntityType;
      var lambdaExpr = BuildSelectByMemberValueArrayLambda(member, mask, orderBy);
      var cmd = new LinqCommand(LinqCommandKind.SpecialSelect, LinqOperation.Select, lambdaExpr);
      cmd.ResultShape = QueryResultShape.EntityList;
      var maskStr = mask.AsHexString();
      cmd.SqlCacheKey = SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialQueryType.SelectByKeyArray, LinqOperation.Select,
                        LockType.None, member.Entity.Name, member.MemberName, mask, orderBy);
      return cmd;
    }
    */

    private static LambdaExpression BuildSelectByKeyValueArrayLambda(EntityMemberInfo member, EntityMemberMask mask = null,
                                          IList<EntityKeyMemberInfo> orderBy = null) {
      var entType = member.Entity.EntityType;
      var listType = typeof(IList<>).MakeGenericType(member.DataType);
      var listPrm = Expression.Parameter(listType, "@list");
      var pred = ExpressionMaker.MakeListContainsPredicate(entType, member, listPrm);
      var entSet = member.Entity.EntitySetConstant;
      var entSetFiltered = ExpressionMaker.MakeCallWhere(entSet, entType, pred);
      var lambda = Expression.Lambda(entSetFiltered, listPrm);
      return lambda;
    }

    public static LambdaExpression BuildChildExistsLambda(EntityMemberInfo refMember) {
      var refInfo = refMember.ReferenceInfo; 
      var child = refInfo.FromMember.Entity;
      var parent = refInfo.ToKey.Entity;
      //build expression
      var sessionPrm = Expression.Parameter(typeof(IEntitySession), "_session");
      var parentPrm = Expression.Parameter(parent.EntityType, "_parent");
      // Build lambda for WHERE
      var cPrm = Expression.Parameter(child.EntityType, "child_");
      var parentRef = Expression.MakeMemberAccess(cPrm, refMember.ClrMemberInfo);
      var eq = Expression.Equal(parentRef, parentPrm);
      var condLambda = Expression.Lambda(eq, cPrm);

      var entSet = ExpressionMaker.MakeEntitySetConstant(child.EntityType);
      var genAny = LinqExpressionHelper.QueryableAny2ArgMethod.MakeGenericMethod(child.EntityType);
      var anyCallExpr = Expression.Call(genAny, entSet, condLambda);

      var lambda = Expression.Lambda(anyCallExpr, parentPrm);
      return lambda;
    }

    private static LambdaExpression BuildSelectFilteredByKeyLambda(EntityKeyInfo key, LockType lockType,
                                     IList<EntityKeyMemberInfo> orderBy, EntityMemberMask mask = null) {
      var entType = key.Entity.EntityType;
      var prms = key.ExpandedKeyMembers.Select(m => Expression.Parameter(m.Member.DataType, "@" + m.Member.MemberName)).ToArray();
      var pred = ExpressionMaker.MakeKeyPredicate(key, prms);
      var entSet = (lockType == LockType.None) ? key.Entity.EntitySetConstant
                                               : ExpressionMaker.MakeEntitySetConstant(entType, lockType);
      var entSetFiltered = ExpressionMaker.MakeCallWhere(entSet, entType, pred);
      var entSetOrdered = entSetFiltered;
      if(orderBy != null && orderBy.Count > 0)
        foreach(var km in orderBy)
          entSetOrdered = ExpressionMaker.AddOrderBy(entSetOrdered, entType, km.Member, km.Desc);
      var lambdaExpr = Expression.Lambda(entSetOrdered, prms);
      return lambdaExpr;
    }


  }
}
