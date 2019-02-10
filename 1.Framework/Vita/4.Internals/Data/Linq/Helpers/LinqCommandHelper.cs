using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using Vita.Entities.Utilities;

namespace Vita.Data.Linq {

  public static class LinqCommandHelper {

    public static void EvaluateLocals(ExecutableLinqCommand command) {
      throw new NotImplementedException();
      /*
      if(command.BaseCommand.Locals.Count == 0)
        return; 
      foreach(var local in command.BaseCommand.Locals) {
        var v = ExpressionHelper.Evaluate(local);
        command.ParamValues.Add(new ParamValue())
      }

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
