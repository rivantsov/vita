using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Linq {

  /* Used for both entity cache queries and DB queries; preprocesses (transforms) query expression
   Tasks:
   1. 'Unfolds' sub-queries referenced through local variables - replaces these references with actual queries
       (analyzer that runs before does the same to include the subqueries into cache key).
   2. Collects all entity sets used in the query (fills Hashset of EntityInfo objects). This set will be later used to determine if the query can be executed 
      in entity cache
   3 - Creates lambda expression from query expression as body and parameters based on local values. 
     For dynamic Linq query: 
       a - Creates parameters for local values (found previously by expression analyzer)
       b - Rewrites query expression replacing local values with parametes   
     For views: 
       Rewrites query expression replacing local values with constant values (Views cannot have parameters).   
    
   */

  public class LinqCommandPreprocessor : ExpressionVisitor {
    EntityModel _model; 
    LinqCommand _command;
    List<ParameterExpression> _parameters;

    public static void PreprocessCommand(EntityModel model, LinqCommand command) {
      if (command.Info.Lambda != null)
        return;
      var preProc = new LinqCommandPreprocessor();
      preProc.Preprocess(model, command); 
    }

    private void Preprocess(EntityModel model, LinqCommand command) {
      _model = model; 
      _command = command;
      _parameters = new List<ParameterExpression>();
      //create parameters
      for (int i = 0; i < _command.Locals.Count; i++) {
        var prmExpr = _command.Locals[i];
        var prm = prmExpr.NodeType == ExpressionType.Parameter ? (ParameterExpression)prmExpr : Expression.Parameter(prmExpr.Type, "@P" + i);
        _parameters.Add(prm);
      }
      var body = this.Visit(_command.Query.Expression);
      _command.Info.Lambda = Expression.Lambda(body, _parameters);
    }

    public override Expression Visit(Expression node) {
      if (node == null)
        return null; 
      //check if it is a local expression - replace with parameter
      var localIndex = _command.Locals.IndexOf(node);
      if(localIndex >= 0) {
        switch(_command.Kind) {
          case LinqCommandKind.DynamicSql:
            //Replace with parameters
            return _parameters[localIndex];
          case LinqCommandKind.View:
          //case LinqCommandKind.PrebuiltQuery:
            return Expression.Constant(_command.ParameterValues[localIndex], node.Type);
        }
      } //switch
      return base.Visit(node); 
    }

    //Detect and retrieve sub-query in local variable
    protected override Expression VisitMember(MemberExpression node) {
      IQueryable subQuery;
      if (EntityQueryUtil.CheckSubQueryInLocalVariable(node, out subQuery)) {
        var newExpr = Visit(subQuery.Expression);
        return newExpr;
      }
      return base.VisitMember(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node) {
      if(node.Method.ReturnsEntitySet()) {
        var entType = node.Type.GetGenericArguments()[0];
        var entInfo = _model.GetEntityInfo(entType, true);
        _command.Info.Entities.Add(entInfo);
        return EntityQueryUtil.CreateEntitySetConstant(entType);  
      }
      if (node.Method.DeclaringType == typeof(EntityQueryExtensions) && node.Method.Name == "Include") {
        return Visit(node.Arguments[0]); //Include lambda was already added to Info.Includes by analyzer
      }
      return base.VisitMethodCall(node);
    }

    protected override Expression VisitConstant(ConstantExpression node) {
      Type entityType; 
      if (IsEntitySet(node, out entityType)) {
        var entInfo = _model.GetEntityInfo(entityType, true);
        _command.Info.Entities.Add(entInfo);
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
