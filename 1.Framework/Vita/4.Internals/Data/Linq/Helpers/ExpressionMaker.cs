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

    public static LambdaExpression MakeInclude(Type entityType, EntityMemberInfo member) {
      var prmEnt = Expression.Parameter(entityType, "@e");
      // Note: have to use MemberAccess here, not MakeGetProperty - IncludeHelper wouldn't understand      
      var getProp = Expression.MakeMemberAccess(prmEnt, member.ClrMemberInfo);
      var lambda = Expression.Lambda(getProp, prmEnt);
      return lambda; 
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

    public static Expression AddOrderBy(Expression expr, Type entType, EntityMemberInfo member, bool desc) {
      var entPrm = Expression.Parameter(entType, "@e");
      var readM = MakeGetProperty(entPrm, member);
      var lambda = Expression.Lambda(readM, entPrm);
      var orderMethod = desc ? LinqExpressionHelper.QueryableOrderByDescMethod : LinqExpressionHelper.QueryableOrderByMethod;
      var genOrderMethod = orderMethod.MakeGenericMethod(entType, member.DataType);
      return Expression.Call(null, genOrderMethod, expr, lambda);
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

    public static LambdaExpression MakeSelectFilteredByKeyArray(EntityMemberInfo member) {
      return null; 
    }

    private static Expression MakeEntitytSetWhereListContains<TEnt, TElem>(IList<TElem> list, string memberName) {
      var entSet = new EntitySet<TEnt>(); 
      var query =  entSet.Where(ent => list.Contains(ExpressionMaker.GetProperty<TEnt, TElem>(ent, memberName)));
      return query.Expression; 
    }

    private static TProp GetProperty<TEnt, TProp>(TEnt ent, string propName) {
      return default(TProp); //not supposed to be called directly
    }
    private static Expression<Func<TEnt, TElem>> ToExpression<TEnt, TElem>(Expression<Func<TEnt, TElem>> func) {
      return func;
    }


    public static LambdaExpression MakeListContainsPredicate_NotUsed(EntityMemberInfo member, ParameterExpression listParam) {
      // Lambda: (ent) -> Queryable.Contains(listParam, EntityHelper.GetProperty(ent, memberName))
      var entType = member.Entity.EntityType;
      var entPrm = Expression.Parameter(entType, "e");
      return null;
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

    public static LambdaExpression MakeListContainsPredicate(Type entType, EntityMemberInfo member, ParameterExpression listPrm) {
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

    public static Expression MakeCallWhere(Expression entSet, Type entType, LambdaExpression pred) {
      var genWhere = _where.MakeGenericMethod(entType);
      return Expression.Call(null, genWhere, entSet, pred); 
    }



    static MethodInfo _where;
    static MethodInfo _containsMethod;
    static MethodInfo _createEntitySetMethod;
    static MethodInfo _convertNullableMethod;
    static MethodInfo _getPropertyStubMethod;

    static ExpressionMaker() {

      _where = FindQueryableWhere();
      var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic; 
      _containsMethod = typeof(Queryable).GetMethods().First(m => m.Name == "Contains" && m.GetParameters().Length == 2);
      _createEntitySetMethod = typeof(ExpressionMaker).GetMethod(nameof(CreateEntitySet), flags);
      _getPropertyStubMethod = typeof(ExpressionMaker).GetMethod(nameof(GetPropertyStub), flags);
      _convertNullableMethod = typeof(ExpressionMaker).GetMethod(nameof(ConvertNullableToValueType), flags);
    }

    private static MethodInfo FindQueryableWhere() {
      var methods = typeof(Queryable).GetMethods().Where(m => m.Name == nameof(Queryable.Where)).ToList();
      // there are 2 methods; get one with lambda (cond) with single parameter
      //public static IQueryable<TSource> Where<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate); // this one!
      // public static IQueryable<TSource> Where<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, int, bool>> predicate);
      foreach(var m in methods) {
        var predType = m.GetParameters()[1].ParameterType;
        var funcType = predType.GetGenericArguments()[0]; //Func<TSource, bool>
        var funcGenArgsCount = funcType.GetGenericArguments().Length;
        if(funcGenArgsCount == 2)
          return m;
      }
      Util.Throw("Failed to find Queryable.Where method.");
      return null;
    } //method

  } //class
}
