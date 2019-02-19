using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Data.Linq {

  public static class ExpressionMaker {
    public static ConstantExpression Const0Int = Expression.Constant(0);
    public static ConstantExpression Const1Int = Expression.Constant(1);
    public static ConstantExpression Const0Long = Expression.Constant(0L);

    public static ConstantExpression CreateConstant(object value, Type type) {
      if (value == null || value.GetType() == type)
        return Expression.Constant(value, type);
      return Expression.Constant(Convert.ChangeType(value, type), type);
    }

    public static ConstantExpression MakeZero(Type type) {
      if(type == typeof(Int64))
        return Const0Long;
      else
        return Const0Int;
    }

    public static BinaryExpression MakeGreaterThanZero(Expression expr) {
      var zeroExpr = MakeZero(expr.Type);
      return Expression.GreaterThan(expr, zeroExpr);
    }

    public static BinaryExpression MakeBinary(ExpressionType nodeType, Expression left, Expression right) {
      // After replacing arguments with column expressions there might be a problem with nullable columns.
      // The following code compensates for this
      if (left.Type == right.Type || left.IsConstNull() || right.IsConstNull())
        return Expression.MakeBinary(nodeType, left, right);
      if (left.Type.CanAssignWithConvert(right.Type))
        return Expression.MakeBinary(nodeType, left, Expression.Convert(right, left.Type));
      if (right.Type.CanAssignWithConvert(left.Type))
        return Expression.MakeBinary(nodeType, Expression.Convert(left, right.Type), right);
      Util.Throw("Cannot create Binary expression for arguments of types {0} and {1}", left.Type, right.Type);
      return null; //never happens
    }

    internal static MemberAssignment MakeMemberAssignment(MemberInfo member, Expression value) {
      var memberType = member.GetMemberReturnType();
      if (memberType != value.Type) {
        if (value.Type.IsNullableOf(memberType)) {
          // Default converter fails in this case   
          //make converter that safely gets underlying value from Nullable type (or Default if null)
          value = MakeSafeNullableConvert(value, memberType);
        } else
          // Make default conversion
          value = Expression.Convert(value, memberType);
      }
      return Expression.Bind(member, value);
    }

    private static Expression MakeSafeNullableConvert(Expression expr, Type targetType) {
      var meth = _convertNullableMethod.MakeGenericMethod(targetType);
      var call = Expression.Call(meth, expr);
      return call;
    }

    public static Expression AddOrderBy(Expression entSet, List<EntityKeyMemberInfo> orderBy) {
      if(orderBy == null || orderBy.Count == 0)
        return entSet;
      var entSetOrdered = entSet;
      foreach(var km in orderBy)
        entSetOrdered = AddOrderBy(entSetOrdered, km.Member, km.Desc);
      return entSetOrdered;
    }

    // targetEntProp is for many-to-many list ordering, when target prop in oder list is on 'target' ent; ex:  
    //     session.EntitySet<IBookAuthor>().OrderBy(ba => ba.Author.LastName)
    // in this case targetEntProp => IBookAuthor.Author
    public static Expression AddOrderBy(Expression entSet, EntityMemberInfo member, bool desc, EntityMemberInfo targetEntProp = null) {
      // Special case - member is entity ref
      if (member.Kind == EntityMemberKind.EntityRef) {
        var orderedEntSet = entSet;
        foreach(var fkm in member.ReferenceInfo.FromKey.ExpandedKeyMembers)
          orderedEntSet = AddOrderBy(orderedEntSet, fkm.Member, desc);
        return orderedEntSet; 
      }
      var entType = entSet.Type.GetGenericArguments()[0];
      var entPrm = Expression.Parameter(entType, "@e");
      Expression ordEnt = entPrm;
      if(targetEntProp != null)
        ordEnt = Expression.MakeMemberAccess(entPrm, targetEntProp.ClrMemberInfo);
      // !! member might be 'hidden' (FK column), so we can't use Expression.MakeMemberAccess as in prior line
      var readProp = MakeGetProperty(ordEnt, member); 
      var lambda = Expression.Lambda(readProp, entPrm);
      var orderMethod = desc ? LinqExpressionHelper.QueryableOrderByDescMethod : LinqExpressionHelper.QueryableOrderByMethod;
      var genOrderMethod = orderMethod.MakeGenericMethod(entType, member.DataType);
      return Expression.Call(null, genOrderMethod, entSet, lambda);
    }

    public static Expression MakeSafeMemberAccess(MemberExpression node) {
      var objType = node.Expression.Type;
      if(!objType.GetTypeInfo().IsInterface)
        return node;
      var ifTest = Expression.Equal(node.Expression, Expression.Constant(null, node.Expression.Type));
      var defaultValueExpr = Expression.Constant(ReflectionHelper.GetDefaultValue(node.Type), node.Type);
      var ifExpr = Expression.Condition(ifTest, defaultValueExpr, node, node.Type);
      return ifExpr;
    }

    private static T ConvertNullableToValueType<T>(Nullable<T> value) where T : struct {
      if (value.HasValue)
        return value.Value;
      else
        return default(T);
    }

    private static Expression MakeEntitytSetWhereListContains<TEnt, TElem>(IList<TElem> list, string memberName) {
      var entSet = new EntitySet<TEnt>(); 
      var query =  entSet.Where(ent => list.Contains(ExpressionMaker.GetProperty<TEnt, TElem>(ent, memberName)));
      return query.Expression; 
    }

    private static TProp GetProperty<TEnt, TProp>(TEnt ent, string propName) {
      return default(TProp); //not supposed to be called directly
    }

    public static LambdaExpression MakeKeyPredicate(EntityKeyInfo key, ParameterExpression[] keyValues) {
      var entType = key.Entity.EntityType;
      var entPrm = Expression.Parameter(entType, "e");
      Expression cond = null;
      for(int i = 0; i < key.ExpandedKeyMembers.Count; i++) {
        var member = key.ExpandedKeyMembers[i].Member;
        var readV = MakeGetProperty(entPrm, member);
        var eq = Expression.MakeBinary(ExpressionType.Equal, readV, keyValues[i]);
        if(cond == null)
          cond = eq;
        else
          cond = Expression.And(cond, eq);
      }
      var pred = Expression.Lambda(cond, entPrm);
      return pred;
    }

    public static LambdaExpression MakeListContainsPredicate(EntityMemberInfo member, ParameterExpression listPrm) {
      var entType = member.Entity.EntityType;
      var entPrm = Expression.Parameter(entType, "@e");
      var containsMethod = typeof(ICollection<>).MakeGenericType(member.DataType).GetMethod("Contains");
      var readV = MakeGetProperty(entPrm, member);
      var callContains = Expression.Call(listPrm, containsMethod, readV);
      var lambda = Expression.Lambda(callContains, entPrm);
      return lambda; 
    }

    public static Expression MakeGetProperty(Expression ent, EntityMemberInfo member) {
      var genMeth = _getPropertyStubMethod.MakeGenericMethod(ent.Type, member.DataType);
      return Expression.Call(null, genMeth, ent, Expression.Constant(member.MemberName));

    }

    internal static TRes GetPropertyStub<TEnt, TRes>(TEnt entity, string propName) {
      return default(TRes); 
    } 

    public static ConstantExpression MakeEntitySetConstant(Type entityType, LockType lockType = LockType.None) {
      var createMeth = _createEntitySetMethod.MakeGenericMethod(entityType);
      var entSet = createMeth.Invoke(null,new object[] { lockType });
      var entSetConst = Expression.Constant(entSet);
      return entSetConst;
    }

    //Do not delete! - used by another CreateEntitySet method
    private static EntitySet<TEntity> CreateEntitySet<TEntity>(LockType lockType) {
      return new EntitySet<TEntity>(null, lockType);
    }

    public static Expression MakeCallWhere(Expression entSet, LambdaExpression pred) {
      var entType = entSet.Type.GetGenericArguments()[0];
      var genWhere = LinqExpressionHelper.QueryableWhereMethod.MakeGenericMethod(entType);
      return Expression.Call(null, genWhere, entSet, pred); 
    }

    public static Expression MakeCallSelect(Expression entSet, LambdaExpression pred) {
      var entType = entSet.Type.GetGenericArguments()[0];
      var outType = pred.Body.Type; 
      var genSelect = LinqExpressionHelper.QueryableSelectMethod.MakeGenericMethod(entType, outType);
      return Expression.Call(null, genSelect, entSet, pred);
    }


    // builds   lambda: (le) => new LinkTuple() { LinkEntity = le, TargetEntity = le.<otherEntRef>})
    public static LambdaExpression MakeNewLinkTupleLambda(EntityMemberInfo targetEntMember) {
      var linkEntType = targetEntMember.Entity.EntityType;
      var lnkPrm = Expression.Parameter(linkEntType, "@lnk");
      var newExpr = Expression.New(LinqExpressionHelper.LinkTupleConstructorInfo);
      var bnd1 = Expression.Bind(LinqExpressionHelper.LinkTupleLinkEntityPropertyInfo, lnkPrm);
      var targetRead = Expression.MakeMemberAccess(lnkPrm, targetEntMember.ClrMemberInfo);
      var bnd2 = Expression.Bind(LinqExpressionHelper.LinkTupleTargetEntityPropertyInfo, targetRead);
      var initExpr = Expression.MemberInit(newExpr, bnd1, bnd2);
      var lambda = Expression.Lambda(initExpr, lnkPrm);
      return lambda;
    }

    static MethodInfo _createEntitySetMethod;
    static MethodInfo _convertNullableMethod;
    static MethodInfo _getPropertyStubMethod;

    static ExpressionMaker() {
      var methods = typeof(Queryable).GetMethods().Where(m => m.Name == nameof(Queryable.Select)).ToList();
      var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic; 
      _createEntitySetMethod = typeof(ExpressionMaker).GetMethod(nameof(CreateEntitySet), flags);
      _getPropertyStubMethod = typeof(ExpressionMaker).GetMethod(nameof(GetPropertyStub), flags);
      _convertNullableMethod = typeof(ExpressionMaker).GetMethod(nameof(ConvertNullableToValueType), flags);
    }
  } //class
}
