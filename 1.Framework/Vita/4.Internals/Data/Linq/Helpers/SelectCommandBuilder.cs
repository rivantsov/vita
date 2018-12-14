﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Vita.Data.Linq;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq {

  public static class SelectCommandBuilder {
    static IList<ParameterExpression> _emptyParamList = new ParameterExpression[] { }; 

    public static QueryInfo BuildSelectByKey(EntityKeyInfo key, LockType lockType, IList<EntityKeyMemberInfo> orderBy = null, EntityMemberMask mask = null) {
      var entType = key.Entity.EntityType;
      var lambdaExpr = BuildSelectFilteredByKeyLambda(key, lockType, orderBy, mask);
      // Explicitly create LinqCommandInfo! we do not want analyzer to do it
      var info = new QueryInfo(Entities.QueryOptions.None, lockType, false /*isView*/, 
             new[] { entType }, entType.Name + "/Select/Key/" + key.ToString(),
             _emptyParamList , null);
      info.Lambda = lambdaExpr;
      info.ResultShape = QueryResultShape.EntityList;
      return info; 
    }

    private static LambdaExpression BuildSelectFilteredByKeyLambda(EntityKeyInfo key, LockType lockType, 
                                     IList<EntityKeyMemberInfo> orderBy,  EntityMemberMask mask = null) {
      var entType = key.Entity.EntityType;
      var prms = key.ExpandedKeyMembers.Select(m => Expression.Parameter(m.Member.DataType, "@" + m.Member.MemberName)).ToArray();
      var pred = ExpressionMaker.MakeKeyPredicate(key, prms);
      var entSet = (lockType == LockType.None) ? key.Entity.EntitySetConstant 
                                               : ExpressionMaker.MakeEntitySetConstant(entType, lockType);
      var entSetFiltered = ExpressionMaker.MakeCallWhere(entSet, entType, pred);
      var entSetOrdered = entSetFiltered;
      if(orderBy != null && orderBy.Count > 0)
        foreach(var km in orderBy)
          entSetOrdered = ExpressionMaker.AddOrderBy(entSetOrdered,  entType, km.Member, km.Desc);
      var lambdaExpr = Expression.Lambda(entSetOrdered, prms);
      return lambdaExpr;
    }

    public static QueryInfo BuildSelectByMemberValueArray(EntityMemberInfo member, EntityMemberMask mask = null) {
      var entType = member.Entity.EntityType; 
      var lambdaExpr = BuildSelectByMemberValueArrayLambda(member, mask);
      var info = new QueryInfo(QueryOptions.None, Entities.Locking.LockType.None, false, //isView
                       new[] { entType }, entType.Name + "/Select/KeyArray/" + member.MemberName,  _emptyParamList, null);
      info.Lambda = lambdaExpr;
      info.ResultShape = QueryResultShape.EntityList;
      return info;

    }

    public static LambdaExpression BuildSelectByMemberValueArrayLambda(EntityMemberInfo member, EntityMemberMask mask = null) {
      var entType = member.Entity.EntityType;
      var listType = typeof(IList<>).MakeGenericType(member.DataType);
      var listPrm = Expression.Parameter(listType, "@list");
      var pred = ExpressionMaker.MakeListContainsPredicate(entType, member, listPrm);
      var entSet = member.Entity.EntitySetConstant;
      var entSetFiltered = ExpressionMaker.MakeCallWhere(entSet, entType, pred);
      var lambda = Expression.Lambda(entSetFiltered, listPrm);
      return lambda; 
    }

    public static QueryInfo BuildGetCountForEntityRef(EntityModel model, EntityReferenceInfo refInfo) {
      var child = refInfo.FromMember.Entity;
      var parent = refInfo.ToKey.Entity;
      var refMember = refInfo.FromMember;
      //build expression
      var sessionPrm = Expression.Parameter(typeof(IEntitySession), "_session");
      var parentInstance = Expression.Parameter(parent.EntityType, "_parent");
      // Build lambda for WHERE
      var cPrm = Expression.Parameter(child.EntityType, "child_");
      var parentRef = Expression.MakeMemberAccess(cPrm, refMember.ClrMemberInfo);
      var eq = Expression.Equal(parentRef, parentInstance);
      var whereLambda = Expression.Lambda(eq, cPrm);
      // 
      var genEntSet = LinqExpressionHelper.SessionEntitySetMethod.MakeGenericMethod(child.EntityType);
      var entSetCall = Expression.Call(sessionPrm, genEntSet);
      var genWhereMethod = LinqExpressionHelper.QueryableWhereMethod.MakeGenericMethod(child.EntityType);
      var whereCall = Expression.Call(genWhereMethod, entSetCall, whereLambda);
      var genCount = LinqExpressionHelper.QueryableCountMethod.MakeGenericMethod(child.EntityType);
      var countCallExpr = Expression.Call(genCount, whereCall);

      var cmd = new EntityCommand(countCallExpr, EntityOperation.Select, child);
      QueryAnalyzer.Analyze(model, cmd);
      QueryPreprocessor.PreprocessCommand(model, cmd);
      cmd.Info.Options |= QueryOptions.NoEntityCache; 
      return cmd.Info;
    }

  }
}