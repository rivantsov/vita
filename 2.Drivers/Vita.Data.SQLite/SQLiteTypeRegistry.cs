using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Entities.Utilities;
using Vita.Data.Driver.TypeSystem;
using System.Data.SQLite;

namespace Vita.Data.SQLite {

  public class SQLiteTypeRegistry : DbTypeRegistry {
    public static string DateTimeFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss.fff";
    public static string TimeFormat = @"hh\:mm\:ss";

    public SQLiteTypeRegistry(SQLiteDbDriver driver) : base(driver) {
      
      // For all int types SQLite provider returns Int64, not sure about actual storage; so we set ColOutType to Int64 here for Int32 type
      var tdInt = AddDbTypeDef("int", typeof(Int32), mapColumnType: false, aliases: "integer");
      MapMany(new[] { typeof(byte), typeof(sbyte), typeof(Int16), typeof(UInt16), typeof(Int32), typeof(UInt32) }, tdInt);

      var tdInt64 = AddDbTypeDef("int64", typeof(Int64));
      MapMany(new[] { typeof(Int64), typeof(UInt64) }, tdInt64);

      var tdReal = AddDbTypeDef("real", typeof(double));
      MapMany(new[] { typeof(Single), typeof(double), typeof(decimal) }, tdReal);

      var tdText = AddDbTypeDef("text", typeof(string));
      MapMany(new[] { typeof(string), typeof(char) }, tdText);
      SpecialTypeDefs[DbSpecialType.String] = tdText;
      SpecialTypeDefs[DbSpecialType.StringAnsi] = tdText;
      SpecialTypeDefs[DbSpecialType.StringUnlimited] = tdText;
      SpecialTypeDefs[DbSpecialType.StringAnsiUnlimited] = tdText;

      var tdBlob = AddDbTypeDef("blob", typeof(byte[]), toLiteral: BytesToLiteral);
      MapMany(new Type[] { typeof(Guid), typeof(Vita.Entities.Binary), typeof(byte[]) }, tdBlob);
      SpecialTypeDefs[DbSpecialType.Binary] = tdBlob;
      SpecialTypeDefs[DbSpecialType.BinaryUnlimited] = tdBlob;

      // SQLite data provider can automatically handle 'date' values, if there's 'date' column association
      // it returns DateTime values from these columns. But in this case it cuts off milliseconds. 
      // So we store dates/time as strings and handle conversion in code; DbReader still sometimes returns value as DateTime - 
      //  we handle it in code.
      // Important - we need to to set type name (affinity) to smth special, not just 'date' - otherwise provider recognizes
      // it and starts converting dates in DbReader and this blows up. Did not find any reliable way to disable this behavior
      var tdDate = AddDbTypeDef("str_date", typeof(string), mapColumnType: false, toLiteral: DateTimeToLiteral );
      Map(typeof(DateTime), tdDate);
      var tdTime = AddDbTypeDef("str_time", typeof(string), mapColumnType: false, toLiteral: TimeSpanToLiteral);
      Map(typeof(TimeSpan), tdTime);
      var tdBool =  AddDbTypeDef("bool", typeof(Int32), mapColumnType: false, toLiteral: BoolToBitLiteral );
      Map(typeof(bool), tdBool);

      Converters.AddConverter<string, TimeSpan>(ParseTimeSpan, TimeSpanToString);
      Converters.AddConverter<Int32, bool>(IntToBool, x => (bool)x ? 1 : 0);
      Converters.AddConverter<string, DateTime>(ParseDateTime, DateTimeToString);
    }

    public override DbTypeInfo GetDbTypeInfo(EntityMemberInfo forMember) {
      if (forMember.Flags.IsSet(EntityMemberFlags.Identity)) {
        return base.GetDbTypeInfo("int64", forMember);
      }
      return base.GetDbTypeInfo(forMember);
    }

    public static object IntToBool(object value) {
      if (value == null || value == DBNull.Value)
        return null;
      switch (value) {
        case Int32 i: return i == 0;
        case Int64 i: return i == 0;
        case bool b: return b;
        default:
          return null;
      }
    }

    public static string BoolToBitLiteral(object value) {
      if (value == null || value == DBNull.Value)
        return "NULL";
      switch(value) {
        case Int32 i: return i == 0 ? "0" : "1";
        case Int64 i: return i == 0 ? "0" : "1";
        case bool b: return b ? "1" : "0";
        default:
          return value + string.Empty; //safe ToSTring()
      }
    }

    public static string BytesToLiteral(object value) {
      Util.CheckParam(value, nameof(value));
      byte[] bytes = DbDriverUtil.GetBytes(value);
      Util.Check(bytes != null, "Bytes to literal: invalid input value type {0}", value.GetType());
      return "x'" + HexUtil.ByteArrayToHex(bytes) + "'";
    }

    public static string DateTimeToLiteral(object value){
      if(value == null || value == DBNull.Value)
        return "NULL";
      var str = DateTimeToString(value);
      return $"'{str}'"; 
    }

    public static object DateTimeToString(object value) {
      if (value == null || value == DBNull.Value)
        return "NULL";
      if (value is string) //SQLite stores dates as string
        return value;
      var dt = (DateTime)value;
      var result = dt.ToString(DateTimeFormat);
      return result;
    }
    public static object ParseDateTime(object value) {
      if (value == null || value == DBNull.Value)
        return null;
      // We store DateTime in db as strings and handle conversions; but suprisingly, sometimes provider/DbReader 'guesses' that string value is date
      // and converts it; this happens sometimes in LINQ. We handle it here
      if (value is DateTime)
        return (DateTime)value;
      var str = (string)value;
      if (DateTime.TryParse(str, out var result))
        return result;
      Util.Throw("Failed to convert string to datetime: {0}", str);
      return null;
    }

    public static string TimeSpanToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var str = TimeSpanToString(value); 
      return $"'{str}'";
    }

    public static string TimeSpanToString(object value) {
      if (value == null || value == DBNull.Value)
        return "NULL";
      if (value is string) // it might be already converted
        return (string) value;
      var ts = (TimeSpan)value;
      var tsStr = ts.ToString(TimeFormat);
      return tsStr;
    }


    public static object ParseTimeSpan(object value) {
      if (value == null || value == DBNull.Value)
        return null;
      // We store Time/Timespan in db as strings and handle conversions; but suprisingly, sometimes provider/DbReader 
      // 'guesses' the type and and converts it; this happens sometimes in LINQ. We handle it here
      if (value is TimeSpan)
        return (TimeSpan)value;
      var str = (string)value;
      if (TimeSpan.TryParse(str, out var result))
        return result;
      Util.Throw("Failed to convert string to TimeSpan: {0}", str);
      return null;
    }

  }//class
}
