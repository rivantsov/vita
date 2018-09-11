using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySql.Data.MySqlClient;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Model;
using Vita.Entities.Logging;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Data.MySql {
  public class MySqlTypeRegistry : DbTypeRegistry {
    public MySqlTypeRegistry(MySqlDbDriver driver)  : base(driver) {
      var none = new Type[] { };
      //numerics
      AddTypeDef("float", MySqlDbType.Float, DbType.Single, typeof(Single), aliases: "float unsigned");
      AddTypeDef("double", MySqlDbType.Double, DbType.Double, typeof(Double), aliases: "real;double unsigned");
      AddTypeDef("decimal", MySqlDbType.Decimal, DbType.Decimal, typeof(Decimal), args: "({0},{1})",
        aliases: "numeric,fixed,dec,decimal unsigned");
      // add another copy with DbType.Currency
      AddTypeDef("decimal", MySqlDbType.Decimal, DbType.Currency, typeof(Decimal), args: "({0},{1})",
                          aliases: "currency", mapToTypes: none);
      //Just to handle props marked with DbType=DbType.Currency
      //AddTypeDef("decimal", MySqlDbType.Decimal, DbType.Currency, typeof(Decimal), args: "({precision},{scale})", flags: DbTypeFlags.IsSubType);

      //integers
      AddTypeDef("int", MySqlDbType.Int32, DbType.Int32, typeof(int),  aliases: "integer");
      AddTypeDef("int unsigned", MySqlDbType.UInt32, DbType.UInt32, typeof(uint));
      AddTypeDef("tinyint", MySqlDbType.Byte, DbType.SByte, typeof(sbyte)); //tinyint in MySql is signed byte - unlike in MSSQL
      AddTypeDef("tinyint unsigned", MySqlDbType.UByte, DbType.Byte, typeof(byte));
      AddTypeDef("smallint", MySqlDbType.Int16,  DbType.Int16, typeof(Int16));
      AddTypeDef("smallint unsigned", MySqlDbType.UInt16, DbType.UInt16, typeof(UInt16));
      AddTypeDef("mediumint", MySqlDbType.Int24, DbType.Int32, typeof(Int32), mapToTypes: none);
      AddTypeDef("bigint", MySqlDbType.Int64, DbType.Int64, typeof(Int64));
      AddTypeDef("bigint unsigned", MySqlDbType.UInt64, DbType.UInt64, typeof(UInt64));
      AddTypeDef("enum", MySqlDbType.Enum, DbType.Int16, typeof(Int16), mapToTypes: none);
      AddTypeDef("set", MySqlDbType.Set, DbType.UInt64, typeof(UInt64), mapToTypes: none);

      // Bool
      AddTypeDef("bit", MySqlDbType.Bit, DbType.Boolean, typeof(ulong), mapToTypes: new Type[] { typeof(bool) });

      // Strings
      AddTypeDef("varchar", MySqlDbType.VarChar, DbType.String, typeof(string), args: "({0})", valueToLiteral: MySqlStringToLiteral);
      AddTypeDef("char", MySqlDbType.String, DbType.StringFixedLength, typeof(string), args: "({0})",
                   mapToTypes: new Type[] { typeof(char) }, valueToLiteral: MySqlStringToLiteral);
      AddTypeDef("tinytext", MySqlDbType.TinyText, DbType.String, typeof(string), mapToTypes: none, 
                     valueToLiteral: MySqlStringToLiteral);
      AddTypeDef("mediumtext", MySqlDbType.MediumText, DbType.String, typeof(string),  flags: DbTypeFlags.Unlimited,
                     mapToTypes: none,  valueToLiteral: MySqlStringToLiteral);
      AddTypeDef("text", MySqlDbType.Text, DbType.String, typeof(string), flags: DbTypeFlags.Unlimited, mapToTypes: none, 
                     valueToLiteral: MySqlStringToLiteral);
      // this maps to unlimited string
      AddTypeDef("longtext", MySqlDbType.LongText, DbType.String, typeof(string), flags: DbTypeFlags.Unlimited, 
        valueToLiteral: MySqlStringToLiteral);


      // Datetime
      AddTypeDef("datetime", MySqlDbType.DateTime, DbType.DateTime, typeof(DateTime), valueToLiteral: DbValueToLiteralConverters.DateTimeToLiteralNoMs);
      AddTypeDef("date", MySqlDbType.Date, DbType.Date, typeof(DateTime), mapToTypes: none, 
                      valueToLiteral: DbValueToLiteralConverters.DateTimeToLiteralNoMs);
      AddTypeDef("time", MySqlDbType.Time, DbType.Time, typeof(TimeSpan), valueToLiteral: DbValueToLiteralConverters.TimeSpanToLiteralNoMs);
      
      AddTypeDef("timestamp", MySqlDbType.Timestamp, DbType.Object, typeof(DateTime), mapToTypes: none);
      AddTypeDef("year", MySqlDbType.Year, DbType.Int16, typeof(Int16), mapToTypes: none);

      // Binaries
      var binTypes = new Type[] { typeof(byte[]), typeof(Vita.Entities.Binary) };
      AddTypeDef("varbinary", MySqlDbType.VarBinary, DbType.Binary, typeof(byte[]), args: "({0})", mapToTypes: binTypes);
      AddTypeDef("binary", MySqlDbType.Binary, DbType.Binary, typeof(byte[]), args: "({0})", mapToTypes: none);
      AddTypeDef("tinyblob", MySqlDbType.TinyBlob, DbType.Binary, typeof(byte[]), mapToTypes: none);
      AddTypeDef("blob", MySqlDbType.Blob, DbType.Binary, typeof(byte[]), flags: DbTypeFlags.Unlimited, mapToTypes: none);
      AddTypeDef("mediumblob", MySqlDbType.MediumBlob, DbType.Binary, typeof(byte[]), flags: DbTypeFlags.Unlimited, mapToTypes: none);
      // serves as unlimited binary
      AddTypeDef("longblob", MySqlDbType.LongBlob, DbType.Binary, typeof(byte[]), flags: DbTypeFlags.Unlimited);

      // Guid - specialized subtype binary(16)
      AddTypeDef("binary", MySqlDbType.Binary, DbType.Binary, typeof(byte[]), args: "(16)", 
        mapToTypes: new Type[] { typeof(Guid) }, dbFirstClrType: typeof(Guid), columnInit: "0", valueToLiteral: BytesToLiteral);

      // the following types should be treated as auto-memo - whenevery property specifies this type explicitly through [Column(TypeName=?)] spec,
      // the column is set to Memo (unlimited size) - unless size is specified explicitly.
      //RegisterAutoMemoTypes("mediumtext", "text", "longtext", "blob", "longblob");

      // bool is stored as UInt64
      Converters.AddConverter<ulong, bool>(LongToBool,  x => x); //this is for MySql bit type

    }


    // bool is stored as UInt64; but comparison results returned as Int64;
    private static object LongToBool(object value) {
      var t = value.GetType();
      if (t == typeof(ulong)) return (ulong)value == 1;
      if (t == typeof(long)) return (long)value == 1;
      return Convert.ToInt32(value) == 1; //should never happen
    }

    public static string MySqlStringToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var str = (string)value;
      //Escape backslash - this is MySql specific
      str = str.Replace(@"\", @"\\");
      if(!str.Contains('\'')) //fast case
        return "'" + str + "'";
      return "'" + str.Replace("'", "''") + "'";
    }

    public override DbColumnTypeInfo GetColumnTypeInfo(EntityMemberInfo member, IActivationLog log) {
      var typeInfo = base.GetColumnTypeInfo(member, log);
      if(member.DataType == typeof(TimeSpan) || member.DataType == typeof(TimeSpan?)) 
        typeInfo.PropertyToColumnConverter = CheckTimeSpanProperty;
      return typeInfo; 
    }

    //MySql does not like too precise timespan values, so we rough it to milliseconds
    private object CheckTimeSpanProperty(object value) {
      if(value == null || value == DBNull.Value)
        return value;
      var ts = (TimeSpan)value;
      return new TimeSpan(ts.Hours, ts.Minutes, ts.Seconds);
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

  }//class
}
