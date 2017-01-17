using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Data;
using System.Collections;
using Vita.Entities;

namespace Vita.Common {
  public static class LoggingExtensions {

    /// <summary>Adds value to Exception.Data dictionary.</summary>
    /// <param name="exception">The target exception.</param>
    /// <param name="name">Value (parameter) name.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="doNotUse">Do not use. Filled automatically with method name and prepended to value name in the dictionary.</param>
    public static void AddValue(this Exception exception, string name, object value, [CallerMemberName] string doNotUse = null) {
      if (exception == null)
        return;
      var varName = name; 
      if (exception.Data.Contains(varName)) //if duplicate, use caller member name
        varName = doNotUse + "/" + name;
      string strValue = value.ToLogString();
      exception.Data[varName] = strValue;
    }

    public static string ToLogString(this object value) {
      string strValue = string.Empty;
      try {
        if (value == null)
          strValue = "(null)";
        else if (value == DBNull.Value)
          strValue = "(DbNull)";
        else if (value is string)
          strValue = '"' +  ((string)value).TrimMiddle(100) + '"';
        else if (value is Exception) {
          var ex = (Exception)value;
          strValue = ex.ToLogString();
        } else if (value is DateTime) {
          var dt = (DateTime)value;
          strValue = "[" + ConvertHelper.DateTimeToUniString(dt) + ", Kind=" + dt.Kind + "]";
        } else if (value is Guid) {
          strValue = "{" + value.ToString() + "}";
        } else if(value is IDbCommand) {
          var cmd = (IDbCommand)value;
          strValue = cmd.ToLogString();
        }else if (value is IDataReader) {
          var rdr = (IDataReader)value;
          strValue = rdr.ToLogString();
        } else if (value is IDataRecord) {
          var dr = (IDataRecord)value;
          strValue = dr.ToLogString();
        /*
        } else if (value is DataTable) {
          var dt = (DataTable)value;
          return dt.ToLogString();
          */
        } else if (value is byte[]) {
          var bytes = (byte[])value;
          strValue = bytes.ToLogString();
        } else if (value is object[]) {
          var arr = (object[])value;
          strValue = arr.ToLogString();
        } else if (value is IList) {
          var arr = ToObjectArray((IList) value);
          strValue = arr.ToLogString();
        } else 
          strValue = value.ToString();
        return strValue;
      } catch (Exception ex) {
        return "LoggingExtensions - catastrophic failure in ToLogString(): " + ex.ToString();
      }
    }

    private static object[] ToObjectArray(IList list) {
      var arr = new object[list.Count];
      for (int i = 0; i < arr.Length; i++)
        arr[i] = list[i];
      return arr; 
    }
    public static string ToLogString(this object[] values) {
      const int MaxTake = 50;
      if (values == null)
        return "(null)";
      var result = string.Join(", ", values.Take(MaxTake).Select(v => v.ToLogString()));
      if (values.Length > MaxTake)
        result += " ...";
      return "[" + result + "]";
    }

    public static string ToLogString(this byte[] values, string prefix = null) {
      const int MaxTake = 64;
      if (values == null)
        return "(null)";
      var bytes = values.Length > MaxTake ? values.Take(MaxTake).ToArray() : values;
      var hex = HexUtil.ByteArrayToHex(bytes); 
      var suffix = (values.Length > MaxTake) ? " ..." : string.Empty;
      return StringHelper.SafeFormat("{0}{1}{2}", prefix, hex, suffix);
    }


    // reports current row
    public static string ToLogString(this IDataReader reader) {
      if (reader == null)
        return "(reader is null)";
      if (reader.IsClosed)
        return "(reader is closed)";
      // check if reader is readable
      IDataRecord rec = reader as IDataRecord;
      var result = StringHelper.SafeFormat("\r\n   DataReader.RecordsAffected= {0}, Depth={1}", reader.RecordsAffected, reader.Depth);
      result += rec.ToLogString();
      return result; 
    }

    public static string ToLogString(this IDataRecord record) {
      if (record == null)
        return "(dataRecord is null)";
      // check if reader is readable
      try {
        var v0 = record[0];
      } catch {
        return "(DataRecord is not readable or empty)";
      }
      // actually read values
      var strValues = new string[record.FieldCount];
      for (int i = 0; i < strValues.Length; i++) {
        var name = record.GetName(i);
        var v = record[i];
        var strV = v.ToLogString();
        strV = strV.TrimMiddle();
        strValues[i] = StringHelper.SafeFormat("    {0} = {1}\r\n", name, strV);
      }
      var result = "  Column values:\r\n" + string.Join(string.Empty, strValues);
      return result;
    }

    /*
    public static string ToLogString(this DataTable table) {
      return StringHelper.SafeFormat("(DataTable, {0} rows)", table.Rows.Count);
    }
    */

    public static string ToLogString(this IDbCommand command) {
      const string template =
@"CommandType: {0}
CommandText: {1}
Parameters: 
{2}
";
      if (command == null) return "(NULL)";
      var sbParams = new StringBuilder();
      foreach (IDbDataParameter prm in command.Parameters)
        sbParams.AppendFormat("    {0} = {1}\r\n", prm.ParameterName, prm.Value.ToLogString());
      return StringHelper.SafeFormat(template, command.CommandType, command.CommandText, sbParams.ToString());
    }

    public static string ToLogString(this ClientFault fault) {
      try {
        if(fault == null)
          return null; 
        var result = StringHelper.SafeFormat("    Fault: {0} Code={1} Tag ={2} Path={3}.", fault.Message, fault.Code, fault.Tag, fault.Path);
        if (fault.Parameters.Count > 0)
          result += string.Join(";", fault.Parameters.Select(kv => StringHelper.SafeFormat("{0}: {1}", kv.Key,  kv.Value.TrimMiddle())));
        return result; 
      } catch(Exception ex) {
        //had been burned by this
        return "LoggingExtensions - catastrophic failure in ToLogString(): " + ex.ToString();
      }

    }

    public static string ToLogString(this Exception exception) {
      try {
        if (exception == null)
          return "(null)";
        var sb = new StringBuilder();
        WriteExceptionLogString(sb, exception);
        return sb.ToString();
      } catch(Exception ex) {
        //had been burned by this
        return "LoggingExtensions - catastrophic failure in ToLogString(): " + ex.ToString() + 
          "\r\n Original exc: \r\n" + exception.ToString();
      }

    }

    private static void WriteExceptionLogString(StringBuilder sb, Exception exception) {
      if (exception is StartupFailureException)
        WriteStartupFailureException(sb, (StartupFailureException)exception);
      if (exception is ClientFaultException)
        WriteClientFaultException(sb, (ClientFaultException)exception);
      else if (exception is OperationAbortException)
        WriteOperationAbortException(sb, (OperationAbortException)exception);
      else if (exception is AggregateException)
        WriteAggregateExceptionLogString(sb, (AggregateException) exception);
      else 
        WriteFatalExceptionLogString(sb, exception); 
    }
    private static void WriteAggregateExceptionLogString(StringBuilder sb, AggregateException exception) {
      sb.AppendFormat("AggregateException: {0} ======================================\r\n", exception.Message);
      sb.AppendLine("Inner exceptions: ");
      foreach(var inner in exception.Flatten().InnerExceptions)
        WriteExceptionLogString(sb, inner);
      sb.AppendLine("End InnerExceptions log =============================================");
    }

    private static void WriteStartupFailureException(StringBuilder sb, StartupFailureException exception) {
      sb.AppendFormat("StartupFailureException: {0}\r\n", exception.Message);
      sb.AppendLine(exception.Log);
      if (exception.Data.Count > 0)
        WriteExceptionData(sb, exception);
      if (exception.InnerException != null)
        WriteInnerException(sb, exception.InnerException);
    }

    private static void WriteClientFaultException(StringBuilder sb, ClientFaultException exception) {
      sb.AppendFormat("ClientFaultException: {0}\r\n", exception.Message);
      foreach(var fault in exception.Faults) {
        sb.Append("    ");
        sb.AppendLine(fault.ToLogString());
      }
      if(exception.Data.Count > 0)
        WriteExceptionData(sb, exception);
      if(exception.InnerException != null)
        WriteInnerException(sb, exception.InnerException); 
    }

    private static void WriteOperationAbortException(StringBuilder sb, OperationAbortException exception) {
      sb.AppendFormat("OperationAborted, reason: {0}, Message: {1} \r\n", exception.ReasonCode, exception.Message);
      if (exception.Data.Count > 0) 
        WriteExceptionData(sb, exception);
      if (exception.InnerException != null)
        WriteInnerException(sb, exception.InnerException);
    }

    // Writes report with Data values and all inner exceptions
    private static void WriteFatalExceptionLogString(StringBuilder sb, Exception exception) {
      var excToStr = exception.ToString();
      sb.AppendLine(excToStr);
      //report inner exception if not reported yet; inner message and stack should be part of exc.ToString()
      var inner = exception.InnerException;
      if (inner != null && !excToStr.Contains(inner.Message))
        WriteInnerException(sb, inner); 
      //report data for exc and inner exceptions
      var ex = exception;
      while(ex != null) {
        WriteExceptionData(sb, ex);
        ex = ex.InnerException;
      }
    }

    private static void WriteInnerException(StringBuilder sb, Exception inner) {
      if (inner == null)
        return;
      sb.AppendLine("   InnerException: ------------------------------------------------");
      sb.AppendLine(inner.ToLogString());
      sb.AppendLine("   EndInnerException: ---------------------------------------------");
    }

    private static void WriteExceptionData(StringBuilder sb, Exception exception) {
      if (exception.Data.Count == 0)
        return;
      sb.AppendFormat("Exception data ({0}): \r\n", exception.GetType());
      foreach (var key in exception.Data.Keys) {
        // Note: we do not use ToLogString for data values - this will trim values to 100 chars;
        // exc.Data dict contains only simple, serializable values. Plus, we do not want to cut-off (trim-middle)
        // long values; we report them as-is.
        sb.AppendFormat("  {0} = {1} \r\n", key, exception.Data[key]);
      }
    }


    // Used by log to trim logged values that are too long 
    public static string TrimMiddle(this string value, int maxLegth = 50, bool removeLineBreaks = true) {
      const string middleEllipsis = "...  ...";
      const int minLength = 10; // middleEllipsis.Length + 2;
      const int ellipsisLenDiv2 = 4;
      if (value == null || value.Length < maxLegth || value.Length < minLength)
        return value;
      if (maxLegth < minLength) maxLegth = minLength;
      var fragmentLen = maxLegth / 2 - ellipsisLenDiv2;
      value = value.Substring(0, fragmentLen) + middleEllipsis + value.Substring(value.Length - fragmentLen);
      if (removeLineBreaks && value.Contains('\n'))
        value = value.Replace('\r', ' ').Replace('\n', ' ');
      return value;
    }

    //Formats array of values as a comma-separated list of values compatible with SQL; used in logging command parameters
    // so that command log entry is directly executable in SQL.
    public static string ToSqlArgList(this object[] parameterValues) {
      if (parameterValues == null || parameterValues.Length == 0) return string.Empty;
      var sArgs = new string[parameterValues.Length];
      for (int i = 0; i < sArgs.Length; i++) {
        var value = parameterValues[i];
        if (value == null) {
          sArgs[i] = "null";
          continue;
        }
        string sValue = null;
        var type = value.GetType();
        if (value == DBNull.Value)
          sValue = "NULL";
        else if (type == typeof(string))
          sValue = value.ToString().Quote();
        else if (type == typeof(Guid))
          sValue = "'" + value.ToString() + "'";
        else if (type == typeof(DateTime))
          sValue = "[" + ((DateTime)value).ToString("s") + "]";
        else if (type.IsEnum)
          sValue = ((int)value).ToString() + "/*" + value.ToString() + "*/";
        else if (type == typeof(byte[])) {
          var bytes = (byte[])value;
          sValue = "'" + bytes.ToLogString() + "'";
        } else
          sValue = value.ToString();
        sValue = sValue.TrimMiddle(50);
        sArgs[i] = sValue;
      }
      return string.Join(", ", sArgs);
    }//method

    public static string FormatSqlParameters(ICollection prms, string format = "{0}={1}", string delimiter = ", ", int maxValueLen = 50) {
      var sValues = new StringList();
      foreach (IDbDataParameter prm in prms)
        sValues.Add(FormatSqlParameter(prm, format, maxValueLen));
      var result = string.Join(delimiter, sValues);
      return result; 
    }

    public static string FormatSqlParameter(IDbDataParameter prm, string format = "{0}={1}", int maxValueLen = 50) {
      var sValue = SqlParameterValueAsString(prm.Value, maxValueLen);
      return StringHelper.SafeFormat(format, prm.ParameterName, sValue);
    }

    public static string SqlParameterValueAsString(object value, int maxLen = 50) {
      if (value == null) 
        return "null";
      if (value == DBNull.Value)
        return "NULL";
      string sValue = null;
      var type = value.GetType();
      if (type == typeof(string))
        sValue = value.ToString().Quote();
      else if (type == typeof(Guid))
        return value.ToString().Quote();
      else if (type == typeof(int) || type == typeof(double))
        return value.ToString();
      else if (type == typeof(DateTime))
        return "[" + ((DateTime)value).ToString("s") + "]";
      else if (type.IsEnum)
        return EnumAsInt(value).ToString() + "/*" + value.ToString() + "*/";
      else if (type == typeof(byte[])) {
        var bytes = (byte[])value;
        sValue = "'" + bytes.ToLogString() + "'";
      } else if (type.IsArray || (type.IsGenericType && value is IList)) {
        var list = (IList)value;
        sValue = "[" + list.ToLogString() + "]";
      /*
      } else if (value is DataTable) {
        var dt = (DataTable)value; 
        return string.Format("-- (@P0: DataTable, RowCount={0})", dt.Rows.Count);
        */
      } else 
        sValue = value.ToLogString();
      sValue = sValue.TrimMiddle(maxLen);
      return sValue;
    }

    private static int EnumAsInt(object value) {
      var enumType = value.GetType();
      if (enumType == typeof(int))
        return (int)value; 
      return Convert.ToInt32(value); 
    }

  }//class
}
