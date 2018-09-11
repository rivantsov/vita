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

namespace Vita.Data.SQLite {

  public class SQLiteTypeRegistry : DbTypeRegistry {
    public static string DateTimeFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss.fff";

    public SQLiteTypeRegistry(SQLiteDbDriver driver) : base(driver) {
      var realTypes = new Type[] {typeof(Single), typeof(double), typeof(decimal)};
      var stringTypes = new Type[] { typeof(string), typeof(char) /* , typeof(DateTime), typeof(TimeSpan)*/  };
      var binTypes = new Type[] { typeof(Guid), typeof(Vita.Entities.Binary), typeof(byte[]) };
      var intTypes = new Type[] {
        typeof(byte), typeof(sbyte), typeof(Int16), typeof(UInt16), typeof(Int32), typeof(UInt32), 
      };
      var int64Types = new Type[] { typeof(Int64), typeof(UInt64) };

      // Note: we associate multiple int types with single storage class, but intercept in particular cases 
      // (in GetDbTypeInfo below), and provide specific DbType values and converters 
      // For all int types MS SQLite provider returns Int64, not sure about actual storage; so we set ColOutType to Int64 here for Int32 type
      AddTypeDef("int", SqliteType.Integer, DbType.Int32, typeof(Int64), mapToTypes: intTypes, dbFirstClrType: typeof(Int32),
                          aliases: "integer");
      AddTypeDef("int64", SqliteType.Integer,DbType.Int64, typeof(Int64), mapToTypes: int64Types, dbFirstClrType: typeof(Int64));
      AddTypeDef("real", SqliteType.Real , DbType.Double, typeof(double), mapToTypes: realTypes, dbFirstClrType: typeof(double));
      AddTypeDef("text", SqliteType.Text, DbType.String, typeof(string), mapToTypes: stringTypes);
      AddTypeDef("blob", SqliteType.Blob, DbType.Binary, typeof(byte[]), mapToTypes: binTypes,
          dbFirstClrType: typeof(Vita.Entities.Binary), valueToLiteral: BytesToLiteral);

      AddTypeDef("date", SqliteType.Text ,DbType.DateTime, typeof(string), mapToTypes: new[] { typeof(DateTime) }, 
                          valueToLiteral: DateTimeToString ); 
      AddTypeDef("time", SqliteType.Text,DbType.DateTimeOffset, typeof(TimeSpan));
      AddTypeDef("bool", SqliteType.Integer,  DbType.Boolean, typeof(Int64), mapToTypes: new[] { typeof(bool) }, 
                          valueToLiteral: BoolToBitLiteral );

      Converters.AddConverter<string, TimeSpan>(x => TimeSpan.Parse((string)x), x => ((TimeSpan)x).ToString("G"));
      Converters.AddConverter<Int64, bool>(x => (Int64)x == 1, null); // x => (bool)x ? 1L : 0L);
      Converters.AddConverter<string, DateTime>(ConvertStringToDateTime, null); // ConvertDateTimeToString);
    }

    //Override base method to ignore DbType and isMemo - match only by ClrType
    public override DbStorageType FindDbTypeDef(DbType dbType, Type clrType, bool isMemo) {
      var match = StorageTypesAll.FirstOrDefault(td => td.MapToTypes.Contains(clrType));
      return match;
    }

    // all db types are registered as non-unlimited, just because there's no difference in SQLite
    public override DbStorageType FindStorageType(Type clrType, bool unlimited) {
      return base.FindStorageType(clrType, false);
    }

    public static string BoolToBitLiteral(object value) {
      if(value == null)
        return "NULL";
      var b = (bool)value;
      return b ? "1" : "0";
    }

    /*
    private static string ConvertBinaryToLiteral(object value) {
      var bytes = (byte[])value;
      return "x'" + HexUtil.ByteArrayToHex(bytes) + "'";
    }
    */
    public static string BytesToLiteral(object value) {
      Util.CheckParam(value, nameof(value));
      switch(value) {
        case Guid g:
          return "x'" + HexUtil.ByteArrayToHex(g.ToByteArray()) + "'";
        case byte[] bytes:
          return "x'" + HexUtil.ByteArrayToHex(bytes) + "'";
        case Binary bin:
          return "x'" + HexUtil.ByteArrayToHex(bin.GetBytes()) + "'";
        default:
          Util.Throw("BytesToLiteral: invalid input value type {0}", value.GetType());
          return null; //never happends
      }
    }

    public static string DateTimeToString(object value){
      if(value == null || value == DBNull.Value)
        return "NULL";
      var dt = (DateTime)value;
      var result = "'" + dt.ToString(DateTimeFormat) + "'";
      return result;
    }

    public static object ConvertStringToDateTime(object v) {
      if(v == null)
        return null;
      var str = (string)v;
     var result = DateTime.ParseExact(str, DateTimeFormat, null, System.Globalization.DateTimeStyles.None);
      return result; 
    }


  }//class
}
