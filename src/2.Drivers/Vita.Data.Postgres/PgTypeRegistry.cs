using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Driver;
using Vita.Data.Model;
using NpgsqlTypes;
using Vita.Entities;
using Vita.Entities.Utilities;
using Vita.Data.Driver.TypeSystem;

namespace Vita.Data.Postgres {
  public class PgTypeRegistry : DbTypeRegistry {
    public PgTypeRegistry(PgDbDriver driver)  : base(driver) {
      var none = new Type[] { };
      
      AddDbTypeDef("real", typeof(Single), aliases: "float4", providerDbType: NpgsqlDbType.Real);
      AddDbTypeDef("double precision", typeof(Double), aliases: "float8", providerDbType: NpgsqlDbType.Double);
      AddDbTypeDef("numeric", typeof(Decimal), DbTypeFlags.PrecisionScale, providerDbType: NpgsqlDbType.Numeric);
      //Just to handle props marked with DbType=DbType.Currency
      AddDbTypeDef("money", typeof(Decimal), mapColumnType: false, providerDbType: NpgsqlDbType.Money, 
        toLiteral: CreateFuncNumToLiteralWithCast("money"));

      //integers
      var tdInt = AddDbTypeDef("int", typeof(Int32), aliases: "integer,oid,int4", providerDbType: NpgsqlDbType.Integer);
      Map(typeof(UInt16), tdInt);

      var tdSmallInt = AddDbTypeDef("smallint", typeof(Int16), providerDbType: NpgsqlDbType.Smallint, 
           toLiteral: CreateFuncNumToLiteralWithCast("smallint"));
      Map(typeof(byte), tdSmallInt);
      Map(typeof(sbyte), tdSmallInt);  
                             
      var tdBigInt = AddDbTypeDef("bigint", typeof(Int64), aliases: "int8", providerDbType: NpgsqlDbType.Bigint,
                                    toLiteral: CreateFuncNumToLiteralWithCast("bigint"));
      Map(typeof(UInt32), tdBigInt);
      Map(typeof(UInt64), tdBigInt);

      // Bool
      AddDbTypeDef("boolean", typeof(bool), providerDbType: NpgsqlDbType.Boolean);
      //Bit
      //TODO: finish Bit data types : http://www.postgresql.org/docs/8.4/static/datatype-bit.html

      // Strings
      var tdVarChar = AddDbTypeDef("character varying", typeof(string), DbTypeFlags.Size, 
                   aliases: "varchar", providerDbType: NpgsqlDbType.Varchar);
      SpecialTypeDefs[DbSpecialType.String] = tdVarChar; 
      SpecialTypeDefs[DbSpecialType.StringAnsi] = tdVarChar; 

      var tdChar = AddDbTypeDef("character", typeof(string), DbTypeFlags.Size, providerDbType: NpgsqlDbType.Char);
      Map( typeof(char), tdChar, size: 1);

      var tdText = AddDbTypeDef("text", typeof(string), flags: DbTypeFlags.Unlimited, providerDbType: NpgsqlDbType.Text);
      SpecialTypeDefs[DbSpecialType.StringUnlimited] = tdText;
      SpecialTypeDefs[DbSpecialType.StringAnsiUnlimited] = tdText; 

      // Datetime
      AddDbTypeDef("timestamp without time zone", typeof(DateTime), providerDbType: NpgsqlDbType.Timestamp);
      AddDbTypeDef("timestamp with time zone", typeof(DateTimeOffset), providerDbType: NpgsqlDbType.TimestampTz);
      AddDbTypeDef("date", typeof(DateTime), mapColumnType: false, providerDbType: NpgsqlDbType.Date);
      AddDbTypeDef("time with time zone", typeof(DateTime), mapColumnType: false, providerDbType: NpgsqlDbType.TimeTz);
      AddDbTypeDef("time without time zone", typeof(TimeSpan), providerDbType: NpgsqlDbType.Time);
      AddDbTypeDef("interval", typeof(TimeSpan), mapColumnType: false, providerDbType: NpgsqlDbType.Interval);

      // Binaries
      // Note: Postgres has special way of presenting binary blobs, so we provide special BytesToLiteral converter
      var tdByteA = AddDbTypeDef("bytea", typeof(byte[]), mapColumnType: false, providerDbType: NpgsqlDbType.Bytea,
            toLiteral: BytesToLiteral);
      Map(typeof(Binary), tdByteA);

      // Guid 
      AddDbTypeDef("uuid", typeof(Guid), toLiteral: GuidToLiteral, providerDbType: NpgsqlDbType.Uuid);
    }

    private static string GuidToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var g = (Guid)value; ;
      return "uuid('" + g.ToString() + "')";
    }

    private static string BytesToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var bytes = DbDriverUtil.GetBytes(value);
      return @"E'\\x" + HexUtil.ByteArrayToHex(bytes) + "'";
    }

    private static Func<object, string> CreateFuncNumToLiteralWithCast(string typeName) {
      return value => {
        if(value == null || value == DBNull.Value)
          return "NULL";
        var strValue = value.ToString();
        return string.Format("CAST({0} AS {1})", strValue, typeName);
      };
    }

    public override string GetDefaultColumnInitExpression(Type type) {
      if(type == typeof(bool))
        return "false";
      return base.GetDefaultColumnInitExpression(type);
    }

  }
}
