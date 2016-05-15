using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Linq.Expressions;

using Vita.Common;
using Vita.Entities.Runtime;

namespace Vita.Entities.Logging {

  // Used for logging queries in full set cache
  public class CacheQueryLogEntry : OperationLogEntry {
    DateTime _dateTime;
    string _expressionString; 
    object[] _args; // command parameters
    long _executionTime;
    int _rowCount;
    private string _toString;

    public CacheQueryLogEntry(OperationContext context, string expressionString, object[] args, DateTime dateTime, long executionTime, int rowCount) 
               : base(LogEntryType.Command, context, dateTime) {
      _expressionString = expressionString; 
      _args = args;
      _dateTime = dateTime; 
      _executionTime = executionTime;
      _rowCount = rowCount;
    }
    public override string ToString() {
      if (_toString == null)
        _toString = FormatEntry(); 
      return _toString;
    }

    private string FormatEntry() {
      var fullFormat = "/*FullSetCache*/ Query: {0}  {1} \r\n-- @{2} ms {3}, {4} */";
      var strArgs = string.Empty;
      // last parameter is always EntitySession; so if we have a single parameter, then no values to log
      if (_args != null && _args.Length > 1) {
        var strValues = new string[_args.Length - 1];
        for (int i = 0; i < _args.Length - 1; i++ )
          strValues[i] =  FormatClosureValues(_expressionString, "@P" + i, _args[i]); 
        strArgs = "\r\n  Parameters: " + string.Join(", ", strValues); 
      }
      var strRowCount = (_rowCount < 0) ? string.Empty : StringHelper.SafeFormat(", {0} row(s)", _rowCount);
      var result = StringHelper.SafeFormat(fullFormat, _expressionString, strArgs, _executionTime, strRowCount, _dateTime);
      return result;
    }

    // For cached queries, local values are passed in closure object. The problem is that a single closure is created for a method, 
    // and a query might use only some of closure values. So for each field in closure we check that it is actually used in the query we report.
    private static string FormatClosureValues(string fromExpression, string paramName, object obj) {
      if (obj == null)
        return paramName + "=null";
      var type = obj.GetType();
      if (!type.IsAnonymousType())
        return paramName + "=" + LoggingExtensions.ToLogString(obj); 
      //Anonymous type
      var fields = type.GetFields();
      var strValues = new StringList();
      for (int i = 0; i < fields.Length; i++ ) {
        var fld = fields[i];
        var fullRefName = paramName + "." + fld.Name;
        if (fromExpression.Contains(fullRefName))
          strValues.Add(fullRefName  + "=" + LoggingExtensions.ToLogString(fld.GetValue(obj)));
      }
      return string.Join(", ", strValues);

    }


  }//DbLogEntry class

}
