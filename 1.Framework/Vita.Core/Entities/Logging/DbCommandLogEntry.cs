using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Data;

using Vita.Common;
using Vita.Entities.Runtime;

namespace Vita.Entities.Logging {

  // The goal here is to relay formatting of the output to the background thread (logging thread), so that the main thread
  // executing commands is not delayed by log formatting.
  public class DbCommandLogEntry : OperationLogEntry {

    DateTime _dateTime;
    string _procCallFormat; // Format for command with parameters, ex:   'EXEC {0} {1}'
    IDbCommand _command; 
    public long ExecutionTime;
    public int RowCount;
    private string _toString;

    public DbCommandLogEntry(OperationContext context, IDbCommand command, string procCallFormat, DateTime dateTime, long executionTime, int rowCount)
                           : base(LogEntryType.Command, context, dateTime) {
      _procCallFormat = procCallFormat;
      _command = command;
      _dateTime = dateTime; 
      ExecutionTime = executionTime;
      RowCount = rowCount; 
    }

    public override string ToString() {
      if (_toString == null)
        _toString = FormatEntry(); 
      return _toString;
    }

    private string FormatEntry() {
      switch(_command.CommandType) {
        case CommandType.StoredProcedure: return FormatStoredProcEntry();
        case CommandType.Text: return FormatSqlEntry(); 
        case CommandType.TableDirect:
        default: 
          return StringHelper.SafeFormat("LOG: unsupported command type {0}, CommandText: {1}. ", _command.CommandType, _command.CommandText); 
      }
    }

    private string FormatStoredProcEntry() {
      if (string.IsNullOrWhiteSpace(_procCallFormat) || !_procCallFormat.Contains("{0}"))
        _procCallFormat = "{0} {1}";
      var fullFormat = _procCallFormat + " -- @{2} ms {3}, {4} "; //exec time, row count, datetime
      var strParams = LoggingExtensions.FormatSqlParameters(_command.Parameters, "{1}" /*Value only*/, ", ", maxValueLen: 50); 
      var strRowCount = (RowCount < 0) ? string.Empty : StringHelper.SafeFormat(", {0} row(s)", RowCount);
      var strDateTime = _dateTime.ToString("[yyyy/MM/dd HH:mm:ss]");
      var result = StringHelper.SafeFormat(fullFormat, _command.CommandText, strParams, ExecutionTime, strRowCount, strDateTime);
      return result; 
    }

    private string FormatSqlEntry() {
      const string fullFormat = "{0} \r\n{1} -- Time {2} ms{3}, {4} \r\n"; //sql, params, exec time, row count, datetime
      var fParams = string.Empty;
      if (_command.Parameters.Count > 0) {
        var strParams = LoggingExtensions.FormatSqlParameters(_command.Parameters, "{0}={1}", ", ", maxValueLen: 50);
        fParams = StringHelper.SafeFormat("-- Parameters: {0} \r\n", strParams);
      }
      var strRowCount = (RowCount < 0) ? string.Empty : StringHelper.SafeFormat("; {0} row(s)", RowCount);
      var strDateTime = _dateTime.ToString("[yyyy/MM/dd HH:mm:ss]");
      var result = StringHelper.SafeFormat(fullFormat, _command.CommandText, fParams, ExecutionTime, strRowCount, strDateTime);
      return result;
    }

  }//DbCommandLogEntry class

}
