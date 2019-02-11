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

namespace Vita.Data.Linq {

  public static class SelectCommandBuilder {
    static IList<ParameterExpression> _emptyParamList = new ParameterExpression[] { }; 

    public static LinqCommand BuildSelectByKey(EntityKeyInfo key, EntityMemberMask mask, LockType lockType, 
                                                          IList<EntityKeyMemberInfo> orderBy = null) {
      var entType = key.Entity.EntityType;
      var lambdaExpr = BuildSelectFilteredByKeyLambda(key, lockType, orderBy, mask);
      var cmd = new LinqCommand(LinqCommandSource.PrebuiltQuery, LinqOperation.Select, lambdaExpr);
      cmd.ResultShape = QueryResultShape.EntityList;
      cmd.MemberMask = mask;
      cmd.LockType = lockType;
      cmd.SqlCacheKey = SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialQueryType.SelectByKey, LinqOperation.Select,
                        lockType, key.Entity.Name, key.Name, mask, orderBy);
      return cmd; 
    }

    public static LinqCommand BuildSelectByMemberValueArray(EntityMemberInfo member, EntityMemberMask mask = null,
                                                          IList<EntityKeyMemberInfo> orderBy = null) {
      var entType = member.Entity.EntityType;
      var lambdaExpr = BuildSelectByMemberValueArrayLambda(member, mask, orderBy);
      var cmd = new LinqCommand(LinqCommandSource.PrebuiltQuery, LinqOperation.Select, lambdaExpr);
      cmd.ResultShape = QueryResultShape.EntityList;
      var maskStr = mask.AsHexString();
      cmd.SqlCacheKey = SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialQueryType.SelectByKeyArray, LinqOperation.Select,
                        LockType.None, member.Entity.Name, member.MemberName, mask, orderBy);
      return cmd;
    }

    private static LambdaExpression BuildSelectByMemberValueArrayLambda(EntityMemberInfo member, EntityMemberMask mask = null,
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

    public static LinqCommand BuildChildExistsForEntityRef(EntityModel model, EntityReferenceInfo refInfo) {
      var child = refInfo.FromMember.Entity;
      var parent = refInfo.ToKey.Entity;
      var refMember = refInfo.FromMember;
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
      var cmd = new LinqCommand(LinqCommandSource.PrebuiltQuery, LinqOperation.Select, lambda);
      cmd.SqlCacheKey = SqlCacheKeyBuilder.BuildSpecialSelectKey(SpecialQueryType.SelectByKeyExists, LinqOperation.Select,
                        LockType.None, child.Name, refMember.MemberName, null, null);
      cmd.Options |= QueryOptions.NoEntityCache; 
      return cmd;
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
