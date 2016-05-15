using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Linq.Expressions;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Logging {
  // Used for logging queries in entity cache cache
  public class CacheCommandLogEntry : OperationLogEntry {
    DateTime _dateTime;
    string _commandName;
    object[] _args; // command parameters
    long _executionTime;
    int _rowCount;
    CacheType _cacheType;
    private string _toString;

    public CacheCommandLogEntry(OperationContext context,  string commandName, object[] args, DateTime dateTime, long executionTime, int rowCount, CacheType cacheType)
                         : base(LogEntryType.Command, context, dateTime) {
      _commandName = commandName;
      _args = args;
      _dateTime = dateTime; 
      _executionTime = executionTime;
      _rowCount = rowCount;
      _cacheType = cacheType;
    }
    public override string ToString() {
      if (_toString == null)
        _toString = FormatEntry(); 
      return _toString;
    }

    private string FormatEntry() {
      var fullFormat = "/*{0}*/ {1} {2} -- @{3} ms {4}, {5}";
      var strArgs = LoggingExtensions.ToSqlArgList(_args);
      var strRowCount = (_rowCount < 0) ? string.Empty : StringHelper.SafeFormat(", {0} row(s)", _rowCount);
      var strCacheType = _cacheType == CacheType.FullSet ? "FullSetCache" : "SparseCache";
      var result = StringHelper.SafeFormat(fullFormat, strCacheType, _commandName, strArgs, _executionTime, strRowCount, _dateTime);
      return result;
    }


  }//DbLogEntry class

}
