using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using Oracle.ManagedDataAccess.Client;
using Vita.Data.Driver;
using Vita.Data.SqlGen;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Data.Oracle {
  public static class OracleConverters {



    public static object IntToBool(object value) {
      if(value == null || value == DBNull.Value)
        return DBNull.Value;
      switch(value) {
        case Int16 i:
          return i == 1;
        case decimal d:
          return d == 1m;
        default:
          return Convert.ToInt16(value) == 1;
      }
    }

    public static object DecimalToBool(object value) {
      if(value == null || value == DBNull.Value)
        return DBNull.Value;
      return (decimal)value != 0m;
    }

    // input value might be bool (mapped to numeric(1), or enum
    public static string NumberToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      switch(value) {
        case bool bv: return bv ? "1" : "0";
        default:
          var str = DbValueToLiteralConverters.DefaultValueToLiteral(value);
          return str; 
          // return value.ToString(); 
      }
    }

    public static string DateTimeFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss.fff";

    public static string DateTimeToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var dt = (DateTime)value;
      var result = "TIMESTAMP '" + dt.ToString(DateTimeFormat) + "'";
      return result;
    }

    public static string IntervalValueFormat = @"d' 'hh':'mm':'ss'.'fff";

    public static string TimeSpanToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var ts = (TimeSpan)value;
      var tsStr = ts.ToString(IntervalValueFormat);
      var result = "INTERVAL'" + tsStr + "' DAY TO SECOND(3)";
      return result;
    }

    public static string BytesToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return SqlTerms.Null.Text; 
      byte[] bytes = DbDriverUtil.GetBytes(value);
      Util.Check(bytes != null, "Bytes to literal: invalid input value type {0}", value.GetType());
      return "hextoraw('" + HexUtil.ByteArrayToHex(bytes) + "')";
    }

  } //class
}
