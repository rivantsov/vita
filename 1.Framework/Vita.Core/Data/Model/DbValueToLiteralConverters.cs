using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Data.Model {

  //Value-to-literal converters are used extensively in batch mode - when calls to stored procedures 
  // are batched into SQL containing calls to procs with literal parameters.
  // NOTE: we can safely assume that vlaue is not null and not DbNull, the calling code checks for this.
  public delegate string DbValueToLiteralConvertFunc(DbTypeInfo type, object value);

  public static class DbValueToLiteralConverters {

    public static DbValueToLiteralConvertFunc GetDefaultToLiteralConverter(Type type) {
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

    public static string StringToLiteral(DbTypeInfo typeDef, object value) {
      var str = (string)value;
      if (!str.Contains('\'')) //fast case
        return "'" + str + "'";
      return "'" + str.Replace("'", "''") + "'";
    }

    public static string BytesToLiteral(DbTypeInfo typeDef, object value) {
      var bytes = (byte[])value;
      return "0x" + HexUtil.ByteArrayToHex(bytes);
    }

    public static string DateTimeToLiteralWithMs(DbTypeInfo typeDef, object value) {
      var dt = (DateTime)value;
      // o: roundtrip date/time pattern, includes milliseconds after dot; used by MS SQL, Postgress
      var result = "'" + dt.ToString("o") + "'";
      return result; 
    }
    public static string TimeSpanToLiteralWithMs(DbTypeInfo typeDef, object value) {
      var ts = (TimeSpan)value;
      var s = ts.ToString(@"hh\:mm\:ss\.FFFFFF"); 
      //special case - if milliseconds is 0, we have an ending dot, and SQL does not like it
      if(s.EndsWith("."))
        s = s + "0"; 
      return "'" + s + "'"; 
    }
    public static string DateTimeToLiteralNoMs(DbTypeInfo typeDef, object value) {
      var dt = (DateTime)value;
      // 's' - Sortable date/time pattern; conforms to ISO 8601. The custom format string is "yyyy'-'MM'-'dd'T'HH':'mm':'ss"
      return "'" + dt.ToString("s") + "'";
    }
    public static string TimeSpanToLiteralNoMs(DbTypeInfo typeDef, object value) {
      var ts = (TimeSpan)value;
      var result = "'" + ts.ToString(@"hh\:mm\:ss") + "'";
      return result; 
    }
    public static string DateTimeOffsetToLiteral(DbTypeInfo typeDef, object value) {
      var dt = (DateTimeOffset)value;
      var result = "'" + dt.ToString("o") + "'";
      return result; 
    }

    public static string GuidToLiteral(DbTypeInfo typeDef, object value) {
      var g = (Guid)value;
      return "'" + g.ToString() + "'";
    }
    public static string BinaryToLiteral(DbTypeInfo typeDef, object value) {
      var bin = (Binary)value;
      return "0x" + HexUtil.ByteArrayToHex(bin.GetBytes());
    }


    public static string DefaultValueToLiteral(DbTypeInfo typeDef, object value) {
      if (value is string)
        return StringToLiteral(typeDef, value);
      if (value is DateTime)
        return DateTimeToLiteralNoMs(typeDef, value); 
      //Do not use value.ToString()! - it uses current culture and may result in strange output
      // For example, if computer has Russian format set in 'Region and Language' dialog, then float numbers like '1.2' result in '1,2'
      // which breaks SQL completely
      return string.Format(CultureInfo.InvariantCulture, "{0}", value);
    }

  }

}
