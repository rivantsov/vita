﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Data;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;

namespace Vita.Entities.Logging {

  // We treat DbCommandLogEntry as subtype of InfoLogEntry, so it ends up in OperationLog just like other 
  //  info log entries. 
  public class DbCommandLogEntry : InfoLogEntry {
    IDbCommand _command; 
    public double ExecutionTime;
    public int RowCount;
    private string _asText;

    public DbCommandLogEntry(LogContext logContext, IDbCommand command, double executionTime, int rowCount) 
         : base (logContext, null) {
      _command = command;
      ExecutionTime = executionTime;
      RowCount = rowCount;
    }

    public override Type BatchGroup => typeof(InfoLogEntry);

    public override string AsText() {
      if(_asText == null)
        _asText = FormatEntry();
      return _asText;
    }

    private string FormatEntry() {
      switch(_command.CommandType) {
        case CommandType.StoredProcedure: return FormatStoredProcEntry();
        case CommandType.Text: return FormatSqlEntry(); 
        case CommandType.TableDirect:
        default: 
          return Util.SafeFormat("LOG: unsupported command type {0}, CommandText: {1}. ", _command.CommandType, _command.CommandText); 
      }
    }

    private string FormatStoredProcEntry() {
      var  fullFormat = "CALL {0} {1}  -- @{2} ms {3}, {4} ";
      var strParams = LoggingExtensions.FormatSqlParameters(_command.Parameters, "{1}" /*Value only*/, ", ", maxValueLen: 50); 
      var strRowCount = (RowCount < 0) ? string.Empty : Util.SafeFormat(", {0} row(s)", RowCount);
      var strDateTime = base.CreatedOn.ToString("[yyyy/MM/dd HH:mm:ss]");
      var result = Util.SafeFormat(fullFormat, _command.CommandText, strParams, ExecutionTime, strRowCount, strDateTime);
      return result; 
    }

    private string FormatSqlEntry() {
      const string fullFormat = "{0} \r\n{1} -- Time {2} ms{3}, {4} \r\n"; //sql, params, exec time, row count, datetime
      var fParams = string.Empty;
      if (_command.Parameters.Count > 0) {
        var strParams = LoggingExtensions.FormatSqlParameters(_command.Parameters, "{0}={1}", ", ", maxValueLen: 50);
        fParams = Util.SafeFormat("-- Parameters: {0} \r\n", strParams);
      }
      var strRowCount = (RowCount < 0) ? string.Empty : Util.SafeFormat("; {0} row(s)", RowCount);
      var strDateTime = base.CreatedOn.ToString("[yyyy/MM/dd HH:mm:ss]");
      var result = Util.SafeFormat(fullFormat, _command.CommandText, fParams, ExecutionTime, strRowCount, strDateTime);
      return result;
    }

  }//DbCommandLogEntry class

}
