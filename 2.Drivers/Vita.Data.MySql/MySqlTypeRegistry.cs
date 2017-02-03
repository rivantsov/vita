using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySql.Data.MySqlClient;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Logging;

namespace Vita.Data.MySql {
  public class MySqlTypeRegistry : DbTypeRegistry {
    public MySqlTypeRegistry(MySqlDbDriver driver)  : base(driver) {
      //numerics
      AddType("float", DbType.Single, typeof(Single), MySqlDbType.Float, aliases: "float unsigned");
      AddType("double", DbType.Double, typeof(Double), MySqlDbType.Double, aliases: "real;double unsigned");
      AddType("decimal", DbType.Decimal, typeof(Decimal), MySqlDbType.Decimal, args: "({precision},{scale})", aliases: "numeric,fixed,dec,decimal unsigned");
      //Just to handle props marked with DbType=DbType.Currency
      AddType("decimal", DbType.Currency, typeof(Decimal), MySqlDbType.Decimal, args: "({precision},{scale})", isDefault: false, isSubType: true);

      //integers
      AddType("int", DbType.Int32, typeof(int), MySqlDbType.Int32, aliases: "integer");
      AddType("int unsigned", DbType.UInt32, typeof(uint), MySqlDbType.UInt32);
      AddType("tinyint", DbType.SByte, typeof(sbyte), MySqlDbType.Byte); //tinyint in MySql is signed byte - unlike in MSSQL
      AddType("tinyint unsigned", DbType.Byte, typeof(byte), MySqlDbType.UByte);
      AddType("smallint", DbType.Int16, typeof(Int16), MySqlDbType.Int16);
      AddType("smallint unsigned", DbType.UInt16, typeof(UInt16), MySqlDbType.UInt16);
      AddType("mediumint", DbType.Int16, typeof(Int32), MySqlDbType.Int24, isDefault: false);
      AddType("bigint", DbType.Int64, typeof(Int64), MySqlDbType.Int64);
      AddType("bigint unsigned", DbType.UInt64, typeof(UInt64), MySqlDbType.UInt64);
      AddType("enum", DbType.Int16, typeof(Int16), MySqlDbType.Enum, isDefault: false);
      AddType("set", DbType.UInt64, typeof(UInt64), MySqlDbType.Set, isDefault: false);

      // Bool
      AddType("bit", DbType.Boolean, typeof(ulong), MySqlDbType.Bit, isDefault: false, clrTypes: new Type[] { typeof(bool) });

      // Strings
      AddType("varchar", DbType.String, typeof(string), MySqlDbType.VarChar, args: "({size})", valueToLiteral: MySqlStringToLiteral);
      AddType("char", DbType.StringFixedLength, typeof(string), MySqlDbType.String, args: "({size})",
                   isDefault: false, clrTypes: new Type[] { typeof(char) }, valueToLiteral: MySqlStringToLiteral);
      AddType("tinytext", DbType.String, typeof(string), MySqlDbType.TinyText, isDefault: false, valueToLiteral: MySqlStringToLiteral);
      AddType("mediumtext", DbType.String, typeof(string), MySqlDbType.MediumText, supportsMemo: true, isDefault: false, valueToLiteral: MySqlStringToLiteral);
      AddType("text", DbType.String, typeof(string), MySqlDbType.Text, supportsMemo: true, isDefault: false, valueToLiteral: MySqlStringToLiteral);
      AddType("longtext", DbType.String, typeof(string), MySqlDbType.LongText, supportsMemo: true, isDefault: false, valueToLiteral: MySqlStringToLiteral);


      // Datetime
      AddType("datetime", DbType.DateTime, typeof(DateTime), MySqlDbType.DateTime, valueToLiteral: DbValueToLiteralConverters.DateTimeToLiteralNoMs);
      AddType("date", DbType.Date, typeof(DateTime), MySqlDbType.Date, isDefault: false, valueToLiteral: DbValueToLiteralConverters.DateTimeToLiteralNoMs);
      AddType("time", DbType.Time, typeof(TimeSpan), MySqlDbType.Time, valueToLiteral: DbValueToLiteralConverters.TimeSpanToLiteralNoMs);
      
      AddType("timestamp", DbType.Object, typeof(DateTime), MySqlDbType.Timestamp, isDefault: false);
      AddType("year", DbType.Int16, typeof(Int16), MySqlDbType.Year, isDefault: false);

      // Binaries
      var binTypes = new Type[] { typeof(Vita.Common.Binary) };
      AddType("varbinary", DbType.Binary, typeof(byte[]), MySqlDbType.VarBinary, args: "({size})", clrTypes: binTypes);
      AddType("binary", DbType.Binary, typeof(byte[]), MySqlDbType.Binary, isDefault: false, args: "({size})", clrTypes: binTypes);
      AddType("tinyblob", DbType.Binary, typeof(byte[]), MySqlDbType.TinyBlob, isDefault: false, clrTypes: binTypes);
      AddType("blob", DbType.Binary, typeof(byte[]), MySqlDbType.Blob, supportsMemo: true, isDefault: false, clrTypes: binTypes);
      AddType("mediumblob", DbType.Binary, typeof(byte[]), MySqlDbType.MediumBlob, supportsMemo: true, isDefault: false, clrTypes: binTypes);
      AddType("longblob", DbType.Binary, typeof(byte[]), MySqlDbType.LongBlob, supportsMemo: true, isDefault: false, clrTypes: binTypes);

      // Guid - specialized subtype binary(16)
      AddType("binary", DbType.Binary, typeof(byte[]), MySqlDbType.Binary, args: "(16)", isDefault: true, isSubType: true,
        clrTypes: new Type[] { typeof(Guid) }, dbFirstClrType: typeof(Guid), columnInit: "0");

      // the following types should be treated as auto-memo - whenevery property specifies this type explicitly through [Column(TypeName=?)] spec,
      // the column is set to Memo (unlimited size) - unless size is specified explicitly.
      RegisterAutoMemoTypes("mediumtext", "text", "longtext", "blob", "longblob");

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

    public static string MySqlStringToLiteral(DbTypeInfo typeDef, object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var str = (string)value;
      //Escape backslash - this is MySql specific
      str = str.Replace(@"\", @"\\");
      if(!str.Contains('\'')) //fast case
        return "'" + str + "'";
      return "'" + str.Replace("'", "''") + "'";
    }

    public override DbTypeInfo GetDbTypeInfo(Entities.Model.EntityMemberInfo member, SystemLog log) {
      var typeInfo = base.GetDbTypeInfo(member, log);
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
  }//class
}
