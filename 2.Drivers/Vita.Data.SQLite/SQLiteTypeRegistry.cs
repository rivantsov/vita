using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Vita.Data.Driver.TypeSystem;

namespace Vita.Data.SQLite {

  public class SQLiteTypeRegistry : DbTypeRegistry {
    public static string DateTimeFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss.fff";

    public SQLiteTypeRegistry(SQLiteDbDriver driver) : base(driver) {
      
      // Note: we associate multiple int types with single storage class, but intercept in particular cases 
      // (in GetDbTypeInfo below), and provide specific DbType values and converters 
      // For all int types MS SQLite provider returns Int64, not sure about actual storage; so we set ColOutType to Int64 here for Int32 type
      var tdInt = AddDbTypeDef("int", typeof(Int64), mapColumnType: false, aliases: "integer");
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

      var tdDate = AddDbTypeDef("date", typeof(string), mapColumnType: false, toLiteral: DateTimeToLiteral );
      Map(typeof(DateTime), tdDate);
      var tdTime = AddDbTypeDef("time", typeof(string), mapColumnType: false, toLiteral: TimeSpanToLiteral);
      Map(typeof(TimeSpan), tdTime);
      var tdBool =  AddDbTypeDef("bool", typeof(Int64), mapColumnType: false, toLiteral: BoolToBitLiteral );
      Map(typeof(bool), tdBool);

      Converters.AddConverter<string, TimeSpan>(x => TimeSpan.Parse((string)x), x => ((TimeSpan)x).ToString("G"));
      Converters.AddConverter<Int64, bool>(x => (Int64)x == 1, null); // x => (bool)x ? 1L : 0L);
      Converters.AddConverter<string, DateTime>(ParseDateTime, DateTimeToString);
    }

    public static string BoolToBitLiteral(object value) {
      if(value == null)
        return "NULL";
      var b = (bool)value;
      return b ? "1" : "0";
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
      var dt = (DateTime)value;
      var result = "'" + dt.ToString(DateTimeFormat) + "'";
      return result;
    }

    public static string TimeSpanToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var ts = (TimeSpan)value;
      var result = "'" + ts.ToString("G") + "'";
      return result;
    }

    public static object DateTimeToString(object value) {
      if(value == null || value == DBNull.Value)
        return DBNull.Value;
      var dt = (DateTime)value;
      var result = dt.ToString(DateTimeFormat);
      return result;
    }

    public static object ParseDateTime(object value) {
      if(value == null || value == DBNull.Value)
        return null;
      var str = (string)value;
     var result = DateTime.ParseExact(str, DateTimeFormat, null, System.Globalization.DateTimeStyles.None);
      return result; 
    }


  }//class
}
