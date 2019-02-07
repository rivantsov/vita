using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Vita.Data.Linq {

  public static class LinqCommandHelper {

    public static void EvaluateParameters(ExecutableLinqCommand command) {
      //We proceed in 2 steps: 
      // 1. We evaluate external parameters (used in lambdas in authorization filters and QueryFilters);
      //    values are in current OperationContext
      // 2. Evaluate local expressions which become final query parameters; they may depend on external params
      /*
      object[] extParamValues = null;
      if(_externalParams.Count > 0) {
        extParamValues = new object[_externalParams.Count];
        for(int i = 0; i < _externalParams.Count; i++)
          extParamValues[i] = LinqExpressionHelper.EvaluateContextParameterExpression(session, _externalParams[i]);
      }
      //Evaluate local expressions
      if(_locals.Count == 0) {
        return;
      } else {
        var extPrmsArr = _externalParams.ToArray();
        _inputValues = _locals.Select(le => ExpressionHelper.Evaluate(le, extPrmsArr, extParamValues)).ToArray();
      }
      */
    }
    private static object[] _emptyArray = new object[] { };


  }//class

}
