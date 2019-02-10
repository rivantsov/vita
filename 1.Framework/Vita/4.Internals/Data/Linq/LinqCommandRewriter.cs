using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq {

  /* Rewrites query expression into canonical form as parameterized lambda. Replaces all 'local' values with 
     parameters.
   Tasks:
   1. 'Unfolds' sub-queries referenced through local variables - replaces these references with actual queries
       (analyzer that runs before does the same to include the subqueries into cache key).
   2. Collects all entity sets used in the query. 
       This set will be later used to determine if the query can be executed in entity cache
   3 - Creates lambda expression from query expression as body and parameters based on local values. 
       a - Creates parameters for local values (found previously by expression analyzer)
       b - Rewrites query expression replacing local values with parametes   
   */

  public class LinqCommandRewriter : ExpressionVisitor {
    EntityModel _model; 
    DynamicLinqCommand _command;
    List<Expression> _locals; 
    List<ParameterExpression> _parameters;

    public static void RewriteToLambda(EntityModel model, DynamicLinqCommand command) {
      var preProc = new LinqCommandRewriter();
      preProc.RewriteCommand(model, command); 
    }

    private void RewriteCommand(EntityModel model, DynamicLinqCommand command) {
      _model = model; 
      _command = command;
      _locals = command.Locals;
      _parameters = new List<ParameterExpression>();
      _parameters.AddRange(command.ExternalParameters); //add original
      //create parameters
      int prmIndex = _parameters.Count;
      for (int i = 0; i < _locals.Count; i++) {
        var local = _locals[i];
        var prm = local.NodeType == ExpressionType.Parameter ? (ParameterExpression)local : Expression.Parameter(local.Type, "@P" + prmIndex++);
        _parameters.Add(prm);
      }
      var body = this.Visit(_command.Expression);
      _command.Lambda = Expression.Lambda(body, _parameters);
    }

    public override Expression Visit(Expression node) {
      if (node == null)
        return null; 
      //check if it is a local expression - replace with parameter
      var localIndex = _locals.IndexOf(node);
      if(localIndex >= 0) {
        return _parameters[localIndex];
      } //switch
      return base.Visit(node); 
    }

    //Detect and retrieve sub-query in local variable
    protected override Expression VisitMember(MemberExpression node) {
      IQueryable subQuery;
      if (LinqExpressionHelper.CheckSubQueryInLocalVariable(node, out subQuery)) {
        var newExpr = Visit(subQuery.Expression);
        return newExpr;
      }
      return base.VisitMember(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node) {
      if(node.Method.IsEntitySetMethod()) {
        var entType = node.Type.GetGenericArguments()[0];
        return ExpressionMaker.MakeEntitySetConstant(entType);  
      }
      if (node.Method.DeclaringType == typeof(EntityQueryExtensions) && node.Method.Name == nameof(EntityQueryExtensions.Include)) {
        return Visit(node.Arguments[0]); //Include lambda was already added to Info.Includes by analyzer
      }
      return base.VisitMethodCall(node);
    }

    protected override Expression VisitConstant(ConstantExpression node) {
      if (IsEntitySet(node, out var dummy)) {
        return node; 
      }
      return base.VisitConstant(node);
    }

    private static bool IsEntitySet(ConstantExpression node, out Type entityType) {
      entityType = null;
      if(node.Type.IsDbPrimitive())
        return false;
      // Check for EntityQuery/EntitySet
      if(typeof(EntityQuery).IsAssignableFrom(node.Type)) {
        var entQuery = (EntityQuery)node.Value;
        if(entQuery.IsEntitySet) {
          entityType = entQuery.ElementType;
          return true;
        }
      }
      return false;
    }//method


  }//class
}//ns
