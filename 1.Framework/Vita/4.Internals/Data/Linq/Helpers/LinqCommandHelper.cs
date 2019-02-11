using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Data.Linq {

  public static class LinqCommandHelper {

    public static void EvaluateLocals(DynamicLinqCommand command) {
      var locals = command.Locals; 
      if(locals.Count == 0) {
        command.LocalValues = _emptyArray; 
        return;
      }
      // evaluate external parameters - they come from OperationContext
      var extValues = _emptyArray;
      if (command.ExternalParameters != null && command.ExternalParameters.Length > 0) {
        var ctx = command.Session.Context;
        extValues = new object[command.ExternalParameters.Length];
        for(int i = 0; i < extValues.Length; i++) {
          extValues[i] = LinqExpressionHelper.EvaluateContextParameterExpression(command.Session, command.ExternalParameters[i]);
        }//for i
      } //if 

      // evaluate locals
      command.LocalValues = new object[locals.Count];
      for(int i = 0; i < locals.Count; i++) {
        var local = locals[i]; 
        command.LocalValues[i] = ExpressionHelper.Evaluate(locals[i], command.ExternalParameters, extValues);
      }
    }

    private static object[] _emptyArray = new object[] { };


  }//class

}
