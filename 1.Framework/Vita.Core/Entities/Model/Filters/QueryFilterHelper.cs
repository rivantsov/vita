using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Entities.Model {

  internal static class QueryFilterHelper {


    public static QueryPredicate CombinePredicatesWithOR(IList<QueryPredicate> filters) {
      if(filters == null || filters.Count == 0)
        return null;
      if(filters.Count == 1)
        return filters[0];
      var allParams = new List<ParameterExpression>();
      var commonEntityParam = filters[0].Lambda.Parameters[0];
      allParams.Add(commonEntityParam);
      var rewriter = new FilterExpressionRewriter();
      //Build new body and new parameter list
      Expression newBody = null;
      foreach(var filter in filters) {
        var filterBody = rewriter.ReplaceParameter(filter.Lambda.Body, filter.Lambda.Parameters[0], commonEntityParam);
        if(newBody == null)
          newBody = filterBody;
        else
          newBody = Expression.Or(newBody, filterBody);
        allParams.AddRange(filter.Lambda.Parameters.Skip(1)); //first param is entity
      }
      var newLambda = Expression.Lambda(newBody, allParams.ToArray());
      var filterType = filters[0].GetType(); 
      var result = (QueryPredicate) Activator.CreateInstance(filterType, newLambda);
      return result;
    }

    // replaces origianl 'entity' parameter with another, 'shared' entity parameter common for all subfilters
    internal class FilterExpressionRewriter : ExpressionVisitor {
      ParameterExpression _parameter;
      ParameterExpression _withParameter;
      public Expression ReplaceParameter(Expression expression, ParameterExpression parameter, ParameterExpression withParameter) {
        _parameter = parameter;
        _withParameter = withParameter;
        return Visit(expression);
      }
      protected override Expression VisitParameter(ParameterExpression node) {
        if(node == _parameter)
          return _withParameter;
        return node;
      }
    } //FilterExpressionRewriter class

  }//class
}
