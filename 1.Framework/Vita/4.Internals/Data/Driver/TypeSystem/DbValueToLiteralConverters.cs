using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Driver;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Data.Driver {

  using DbValueToLiteralFunc = Func<object, string>;

  public static class DbValueToLiteralConverters {

    public static DbValueToLiteralFunc GetDefaultToLiteralConverter(Type type) {
      if(type == typeof(string))
        return DbValueToLiteralConverters.StringToLiteral;
      if(type == typeof(Guid))
        return DbValueToLiteralConverters.GuidToLiteral;
      if(type == typeof(DateTime))
        return DbValueToLiteralConverters.DateTimeToLiteralWithMs;
      if(type == typeof(TimeSpan))
        return DbValueToLiteralConverters.TimeSpanToLiteralWithMs;
      if(type == typeof(DateTimeOffset))
        return DbValueToLiteralConverters.DateTimeOffsetToLiteral;
      if(type == typeof(byte[]))
        return DbValueToLiteralConverters.BytesToLiteral;
      if(type == typeof(Binary))
        return DbValueToLiteralConverters.BinaryToLiteral;
      return DbValueToLiteralConverters.DefaultValueToLiteral;
    }

    public static string StringToLiteral(object value) {
      var str = (string)value;
      if (!str.Contains('\'')) //fast case
        return "'" + str + "'";
      return "'" + str.Replace("'", "''") + "'";
    }

    public static string BytesToLiteral(object value) {
      Util.CheckParam(value, nameof(value));
      switch(value) {
        case Guid g:
          return "0x" + HexUtil.ByteArrayToHex(g.ToByteArray());
        case byte[] bytes:
          return "0x" + HexUtil.ByteArrayToHex(bytes);
        case Binary bin:
          return "0x" + HexUtil.ByteArrayToHex(bin.GetBytes()); 

        default:
          Util.Throw("BytesToLiteral: invalid input value type {0}", value.GetType());
          return null; //never happends
      }
    }

    public static string DateTimeToLiteralWithMs(object value) {
      var dt = (DateTime)value;
      // o: roundtrip date/time pattern, includes milliseconds after dot; used by MS SQL, Postgress
      var result = "'" + ConvertHelper.DateTimeToUniString(dt) + "'";
      return result; 
    }
    public static string TimeSpanToLiteralWithMs(object value) {
      var ts = (TimeSpan)value;
      var s = ts.ToString(@"hh\:mm\:ss\.FFFFF"); 
      //special case - if milliseconds is 0, we have an ending dot, and SQL does not like it
      if(s.EndsWith("."))
        s = s + "0"; 
      return "'" + s + "'"; 
    }
    public static string DateTimeToLiteralNoMs(object value) {
      var dt = (DateTime)value;
      // 's' - Sortable date/time pattern; conforms to ISO 8601. The custom format string is "yyyy'-'MM'-'dd'T'HH':'mm':'ss"
      return "'" + dt.ToString("s") + "'";
    }
    public static string TimeSpanToLiteralNoMs(object value) {
      var ts = (TimeSpan)value;
      var result = "'" + ts.ToString(@"hh\:mm\:ss") + "'";
      return result; 
    }
    public static string DateTimeOffsetToLiteral(object value) {
      var dt = (DateTimeOffset)value;
      var result = "'" + dt.ToString("o") + "'";
      return result; 
    }

    public static string GuidToLiteral(object value) {
      var g = (Guid)value;
      return "'" + g.ToString() + "'";
    }
    public static string BinaryToLiteral(object value) {
      var bin = (Binary)value;
      return "0x" + HexUtil.ByteArrayToHex(bin.GetBytes());
    }


    public static string DefaultValueToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      if (value is string)
        return StringToLiteral(value);
      if (value is DateTime)
        return DateTimeToLiteralNoMs(value);
      var type = value.GetType();
      if (type.IsEnum) {
        var intValue = (int) Convert.ChangeType(value, typeof(int));
        return intValue.ToString(CultureInfo.InvariantCulture); 
      }

      //Do not use value.ToString()! - it uses current culture and may result in strange output
      // For example, if computer has Russian format set in 'Region and Language' dialog, then float numbers like '1.2' result in '1,2'
      // which breaks SQL completely
      return string.Format(CultureInfo.InvariantCulture, "{0}", value);
    }


  }

}
