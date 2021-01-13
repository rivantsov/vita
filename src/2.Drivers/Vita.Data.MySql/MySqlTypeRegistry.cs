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
using Vita.Data.Driver.TypeSystem;

namespace Vita.Data.MySql {
  public class MySqlTypeRegistry : DbTypeRegistry {

    public MySqlTypeRegistry(MySqlDbDriver driver)  : base(driver) {
     
      AddDbTypeDef("float", typeof(Single), aliases: "float unsigned");
      AddDbTypeDef("double", typeof(Double), aliases: "real;double unsigned");
      AddDbTypeDef("decimal", typeof(Decimal), DbTypeFlags.PrecisionScale, aliases: "numeric,fixed,dec,decimal unsigned");

      //integers
      AddDbTypeDef("int", typeof(int),  aliases: "integer");
      AddDbTypeDef("int unsigned", typeof(uint));
      AddDbTypeDef("tinyint", typeof(sbyte)); //tinyint in MySql is signed byte - unlike in MSSQL
      AddDbTypeDef("tinyint unsigned", typeof(byte));
      AddDbTypeDef("smallint", typeof(Int16));
      AddDbTypeDef("smallint unsigned", typeof(UInt16));
      AddDbTypeDef("mediumint", typeof(Int32), mapColumnType: false);
      AddDbTypeDef("bigint", typeof(Int64));
      AddDbTypeDef("bigint unsigned", typeof(UInt64));
      AddDbTypeDef("enum", typeof(Int16), mapColumnType: false);
      AddDbTypeDef("set", typeof(UInt64), mapColumnType: false);

      // Bool
      var bitTd = AddDbTypeDef("bit", typeof(ulong), mapColumnType: false);
      Map(typeof(bool), bitTd);  

      // Strings
      var tdVarChar= AddDbTypeDef("varchar", typeof(string), DbTypeFlags.Size, toLiteral: MySqlStringToLiteral);
      base.SpecialTypeDefs[DbSpecialType.String] = base.SpecialTypeDefs[DbSpecialType.StringAnsi] = tdVarChar;

      var tdChar = AddDbTypeDef("char", typeof(string), DbTypeFlags.Size, mapColumnType: false, toLiteral: MySqlStringToLiteral);
      Map(typeof(char), tdChar, size: 1);

      AddDbTypeDef("tinytext", typeof(string), mapColumnType: false, toLiteral: MySqlStringToLiteral);
      AddDbTypeDef("mediumtext", typeof(string),  flags: DbTypeFlags.Unlimited, mapColumnType: false, 
                     toLiteral: MySqlStringToLiteral);
      AddDbTypeDef("text", typeof(string), flags: DbTypeFlags.Unlimited, mapColumnType: false, toLiteral: MySqlStringToLiteral);
      // this maps to unlimited string
      var tdLText = AddDbTypeDef("longtext", typeof(string), flags: DbTypeFlags.Unlimited, toLiteral: MySqlStringToLiteral);
      base.SpecialTypeDefs[DbSpecialType.StringUnlimited] = base.SpecialTypeDefs[DbSpecialType.StringAnsiUnlimited] = tdLText;


      // Datetime
      AddDbTypeDef("datetime", typeof(DateTime), toLiteral: DbValueToLiteralConverters.DateTimeToLiteralNoMs);
      AddDbTypeDef("date", typeof(DateTime), mapColumnType: false, toLiteral: DbValueToLiteralConverters.DateTimeToLiteralNoMs);
      AddDbTypeDef("time", typeof(TimeSpan), toLiteral: DbValueToLiteralConverters.TimeSpanToLiteralNoMs);
      
      AddDbTypeDef("timestamp", typeof(DateTime), mapColumnType: false);
      AddDbTypeDef("year", typeof(Int16), mapColumnType: false);

      // Binaries
      AddDbTypeDef("varbinary", typeof(byte[]), DbTypeFlags.Size, specialType: DbSpecialType.Binary);
      var tdBin = AddDbTypeDef("binary", typeof(byte[]), DbTypeFlags.Size, mapColumnType: false);
      AddDbTypeDef("tinyblob", typeof(byte[]), mapColumnType: false);
      AddDbTypeDef("blob", typeof(byte[]), flags: DbTypeFlags.Unlimited, mapColumnType: false);
      AddDbTypeDef("mediumblob", typeof(byte[]), flags: DbTypeFlags.Unlimited, mapColumnType: false);
      // serves as unlimited binary
      AddDbTypeDef("longblob", typeof(byte[]), flags: DbTypeFlags.Unlimited, columnInit: "0",
        specialType: DbSpecialType.BinaryUnlimited);

      // Guid - specialized subtype binary(16)
      Map(typeof(Guid), tdBin, size: 16); 

      // bool is stored as UInt64
      Converters.AddConverter<ulong, bool>(LongToBool,  x => x); //this is for MySql bit type

    }

    public override DbTypeDef GetDbTypeDef(Type dataType) {
      if(dataType == typeof(Binary))
        dataType = typeof(byte[]);
      return base.GetDbTypeDef(dataType);
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

    public static string BytesToLiteral(object value) {
      Util.CheckParam(value, nameof(value));
      var str = "0x" + HexUtil.ByteArrayToHex(DbDriverUtil.GetBytes(value));
      return str; 
    }

  }//class
}
