using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Reflection;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities.Runtime;
using Vita.Entities.Model;
using Vita.Entities.Linq;
using Vita.Data;

namespace Vita.Entities.Caching {
  // Rewrites the query to execute against cached entity lists. 
  // The final form of compiled query function: 
  //    Func<EntitySession, EntityCache, object[], object>
  // Makes the following rewrites:
  //  1. Replaces references to entity sets with references to cached entity lists.
  //  2. Replaces calls to Queryable methods with matching methods of Enumerable class.
  //  3. Injects calls to CloneEntity(ies) methods using a separate visit loop in CloneCallInjector 
  public class CacheQueryRewriter: ExpressionVisitor {
    EntityModel _model; 
    LambdaExpression _lambda;
    StringCaseMode _caseMode; 
    ParameterExpression _sessionParam; 
    ParameterExpression _cacheParam;
    ParameterExpression _argsParam; //array of parameters of original LINQ lambda

    public CacheQueryRewriter(EntityModel model, StringCaseMode caseMode) {
      _model = model;
      _caseMode = caseMode; 
    }

    public Func<EntitySession, FullSetEntityCache, object[], object> Rewrite(LambdaExpression lambda) {
      _lambda = lambda;
      _sessionParam = Expression.Parameter(typeof(EntitySession), "@session");
      _cacheParam = Expression.Parameter(typeof(FullSetEntityCache), "@entitycache");
      _argsParam = Expression.Parameter(typeof(object[]), "@args");
      var newParams = new ParameterExpression[] { _sessionParam, _cacheParam, _argsParam };
      // Rewrite
      var newBody = Visit(lambda.Body);
      // we need a separate visiting loop to inject Cloning method
      var injector = new CloneCallInjector();
      var finalBody = injector.InjectCloneMethod(_model, newBody, _sessionParam);
      var finalBodyToObj = Expression.Convert(finalBody, typeof(object));
      //Wrap into lambda
      var newLambda = Expression.Lambda(finalBodyToObj, newParams);
      var compiled = newLambda.Compile();
      var result = (Func<EntitySession, FullSetEntityCache, object[], object>)compiled;
      return result;
    }

    protected override Expression VisitConstant(ConstantExpression node) {
      var entQuery = node.Value as EntityQuery;
      if(entQuery != null && entQuery.IsEntitySet) {
        var genMethod = EntityCacheHelper.GetEntityListMethod.MakeGenericMethod(entQuery.ElementType);
        var listGetExpr = Expression.Call(_cacheParam, genMethod);
        return listGetExpr;
      }
      // otherwise call base
      return base.VisitConstant(node);
    }
    protected override Expression VisitParameter(ParameterExpression node) {
      var index = _lambda.Parameters.IndexOf(node);
      if(index >= 0) {
        var indexExpr = Expression.Constant(index);
        var getAtIndex = Expression.Convert(Expression.ArrayAccess(_argsParam, indexExpr), node.Type);
        return getAtIndex;
      }
      return base.VisitParameter(node);
    }

    protected override Expression VisitBinary(BinaryExpression node) {
      switch(node.NodeType) {
        case ExpressionType.Equal:
        case ExpressionType.NotEqual:
          if(_model.IsEntity(node.Left.Type) || _model.IsEntity(node.Right.Type)) {
            //visit children and replace with EntitiesEqual
            var newLeft = Visit(node.Left);
            var newRight = Visit(node.Right);
            var method = node.NodeType == ExpressionType.Equal ? EntityCacheHelper.EntitiesEqualMethod : EntityCacheHelper.EntitiesNotEqualMethod;
            return Expression.Call(null, method, newLeft, newRight);
          }//if
          // compare both args, to cover expr like 'p.Name == null'
          if (node.Left.Type == typeof(string) && node.Right.Type == typeof(string) && _caseMode == StringCaseMode.CaseInsensitive) {
            var baseLeft = base.Visit(node.Left);
            var baseRight = base.Visit(node.Right);
            Expression result = Expression.Call(EntityCacheHelper.StringStaticEquals3Method, 
                                                 baseLeft, baseRight, EntityCacheHelper.ConstInvariantCultureIgnoreCase);
            if (node.NodeType == ExpressionType.NotEqual)
              result = Expression.Not(result);
            return result; 
          }
          break;
      }//switch
      return base.VisitBinary(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node) {
      //Check if it is WithOptions method
      if(node.Method.DeclaringType == typeof(EntityQueryExtensions))
        return Visit(node.Arguments[0]);
      //Check queryable method, replace with Enumerable equivalent
      var enumMethod = QueryableToEnumerableMapping.GetGenericEnumerableFor(node.Method);
      if(enumMethod != null) {
        //replace with Enumerable.cs method
        var args = new Expression[node.Arguments.Count];
        for(int i = 0; i < args.Length; i++)
          args[i] = ExpressionHelper.Unqoute(this.Visit(node.Arguments[i]));
        var enumCallNode = Expression.Call(enumMethod, args);
        return enumCallNode;
      }
      // Check if it is string.StartsWith method - change to case insensitive version
      if (node.Method == EntityCacheHelper.StringStartsWith1Method && _caseMode == StringCaseMode.CaseInsensitive) {
        //Change s.StartsWith(substr) to s.StartsWith(substr, String.Comparison.InvariantCultureIgnoreCase)
        var nodeObj = base.Visit(node.Object);
        var nodeArg = base.Visit(node.Arguments[0]);
        return Expression.Call(nodeObj, EntityCacheHelper.StringStartsWith2Method, nodeArg, EntityCacheHelper.ConstInvariantCultureIgnoreCase);
      }
      //default
      return base.VisitMethodCall(node);
    }

    protected override Expression VisitMember(MemberExpression node) {
      var result = base.VisitMember(node);
      var memberExpr = result as MemberExpression;
      if(memberExpr != null)
        result = ExpressionHelper.MakeSafeMemberAccess(memberExpr);
      return result;
    }



    #region CloneCallInjector nested class

    #region comments
    // Entities returned from a query must be attached to the entity session that was used for the query. Enitities in cache 
    // are attached to a special entity session that was used to load the entities into the cache. 
    // Therefore, when we run a query against cached entities, we need to clone the resulting entities, and attach the clones
    // to the session that fired the query. 
    // The following class injects the interceptor call into the query expression. 
    // Note that not all queries return entities - some return primitive values like Count, 
    // or anonymous objects consisting of simple properties - we don't need any interception for these.
    // We look at the query expression and its nodes, from top to bottom, trying to find the appropriate place to add the 
    // interceptor - a call to CloneEntity(ies) method. We look at node return type and decide between one of possibilities:
    //   - no interceptor needed for this or any child entities (node result in a primitive type like string);
    //   - interceptor is needed here (node result is entity or entity list) - wrap the node in CloneEntity(ies) call and return the wrapped node.
    //   - node returns some complex object that might contain entities inside - continue visiting/analyzing child expressions of the node. 
    //  We look at 3 spots as interception points: 
    //    - the top node of the query - if the query returns entity(ies), we simply clone and attach them
    //    - New expression - arguments passed the the constructor might contain entities, so we add interceptors to the argument expressions
    //    - Body of lambda in 'Select' method call. 
    //  Note that #3 is a special case that is not always covered by #2. Example: 'from b in books select b.Authors' - returns lists of lists
    //  of authors, but there's no New expression involved. The only place we can inject the clone method is into the result of the lambda
    //   'b=>b.Authors'.
    #endregion

    /// <summary> Injects an call to a method that clones the returned entities and attaches them to the calling session.</summary>
    internal class CloneCallInjector : ExpressionVisitor {
      EntityModel _model; 
      ParameterExpression _sessionParameter;

      public Expression InjectCloneMethod(EntityModel model, Expression queryExpression, ParameterExpression sessionParameter) {
        _model = model;
        _sessionParameter = sessionParameter;
        var newExpr = AnalyzeNode(queryExpression);
        return newExpr;
      }

      protected override Expression VisitMethodCall(MethodCallExpression node) {
        var method = node.Method;
        if(method.DeclaringType == typeof(Enumerable) && method.Name == "Select") {
          var lambda = (LambdaExpression)node.Arguments[1];
          var newBody = AnalyzeNode(lambda.Body);
          var newLambda = Expression.Lambda(newBody, lambda.Parameters);
          return Expression.Call(null, method, node.Arguments[0], newLambda);
        }
        return base.VisitMethodCall(node);
      }

      protected override Expression VisitNew(NewExpression node) {
        //go thru argements
        var newArgs = new List<Expression>();
        foreach(var arg in node.Arguments) {
          var newArg = AnalyzeNode(arg);
          newArgs.Add(newArg);
        }//foreach
        return Expression.New(node.Constructor, newArgs);
      }

      private Expression AnalyzeNode(Expression node) {
        var nodeType = node.Type;
        // quickly detect some simple cases
        if(nodeType.IsDbPrimitive() || nodeType.IsListOfDbPrimitive())
          return node;
        if(_model.IsEntity(nodeType)) {
          var cloneMethod = EntityCacheHelper.CloneEntityMethod.MakeGenericMethod(node.Type);
          return Expression.Call(cloneMethod, _sessionParameter, node);
        }
        if(nodeType.IsEntitySequence()) {
          var entType = node.Type.GenericTypeArguments[0];
          var cloneMethod = EntityCacheHelper.CloneEntitiesMethod.MakeGenericMethod(entType);
          var callExpr = Expression.Call(cloneMethod, _sessionParameter, node);
          return callExpr;
        }
        // otherwise continue visiting children
        return Visit(node);
      }
    }// CloneCallInjector class
    #endregion

  }//class

}
