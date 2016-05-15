using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Linq {

  public static class EntityQueryUtil {

    public static bool IsSet(this LinqCommandFlags flags, LinqCommandFlags flag) {
      return (flags & flag) != 0; 
    }

    public static bool CheckSubQueryInLocalVariable(MemberExpression node, out IQueryable subQuery) {
      if (node.Expression != null && node.Expression.NodeType == ExpressionType.Constant && node.Type.IsGenericQueryable()) {
        var objExpr = (ConstantExpression)node.Expression;
        subQuery = (IQueryable)node.Member.GetMemberValue(objExpr.Value);
        return true; 
      }
      subQuery = null; 
      return false; 
    }

    public static bool ReturnsEntitySet(this MethodInfo method) {
      if(method.Name != "EntitySet" || !method.ReturnType.IsGenericType)
        return false;
      switch(method.DeclaringType.Name) {
        case "IEntitySession": case "EntitySession": case "ViewHelper": case "LockHelper": 
          return true; 
        default:
          return false; 
      }
    }

    static MethodInfo _createMethod; 
    public static ConstantExpression CreateEntitySetConstant(Type entityType) {
      _createMethod = _createMethod ?? typeof(EntityQueryUtil).GetMethod("CreateEntitySet", BindingFlags.Static | BindingFlags.NonPublic);
      var genMethod = _createMethod.MakeGenericMethod(entityType);
      var result = genMethod.Invoke(null, null);
      return Expression.Constant(result);
    }
    //Do not delete! = used by another CreateEntitySet method
    private static EntitySet<TEntity> CreateEntitySet<TEntity>() {
      return new EntitySet<TEntity>();
    }


  }//class
}
