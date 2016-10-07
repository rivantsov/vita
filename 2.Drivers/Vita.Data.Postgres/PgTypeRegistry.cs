using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data.Driver;
using Vita.Data.Model;
using NpgsqlTypes;

namespace Vita.Data.Postgres {
  public class PgTypeRegistry : DbTypeRegistry {
    public PgTypeRegistry(PgDbDriver driver)  : base(driver) {
      AddType("real", DbType.Single, typeof(Single), NpgsqlDbType.Real);
      AddType("double precision", DbType.Double, typeof(Double), NpgsqlDbType.Double);
      AddType("numeric", DbType.Decimal, typeof(Decimal), NpgsqlDbType.Numeric, args: "({precision},{scale})");
      //Just to handle props marked with DbType=DbType.Currency
      AddType("money", DbType.Currency, typeof(Decimal), NpgsqlDbType.Money, isDefault: false, valueToLiteral: NumToLiteralWithCast);

      //integers
      // Note: We have to provide explicit cast for certain int and currency types; otherwise, function overload resolution does not match the intended function 
      // (it works by function name and arg types match). Quite annoying. 
      AddType("int", DbType.Int32, typeof(int), NpgsqlDbType.Integer, aliases: "integer,oid", clrTypes: new Type[] { typeof(UInt16) });
      AddType("smallint", DbType.Int16, typeof(Int16), NpgsqlDbType.Smallint, clrTypes: new Type[] { typeof(byte), typeof(sbyte) }, valueToLiteral: NumToLiteralWithCast);
      AddType("bigint", DbType.Int64, typeof(Int64), NpgsqlDbType.Bigint, clrTypes: new Type[] { typeof(UInt32), typeof(UInt64) }, valueToLiteral: NumToLiteralWithCast);
      //AddType("serial", DbType.Int32, typeof(Int32), NpgsqlDbType ??);

      // Bool
      AddType("boolean", DbType.Boolean, typeof(bool), NpgsqlDbType.Boolean);
      //Bit
      //TODO: finish Bit data types : http://www.postgresql.org/docs/8.4/static/datatype-bit.html

      // Strings
      AddType("character varying", DbType.String, typeof(string), NpgsqlDbType.Varchar, args: "({size})");
      AddType("character", DbType.StringFixedLength, typeof(string), NpgsqlDbType.Char, args: "({size})",
        isDefault: false, clrTypes: new Type[] { typeof(char) }, aliases: "char");
      AddType("text", DbType.String, typeof(string), NpgsqlDbType.Text, supportsMemo: true, isDefault: false);
      // Datetime
      AddType("timestamp without time zone", DbType.DateTime, typeof(DateTime), NpgsqlDbType.Timestamp);
      AddType("timestamp with time zone", DbType.Date, typeof(DateTimeOffset), NpgsqlDbType.TimestampTZ);
      AddType("date", DbType.Date, typeof(DateTime), NpgsqlDbType.Date, isDefault: false, aliases: "date without time zone");
      AddType("time", DbType.Time, typeof(TimeSpan), NpgsqlDbType.Time, aliases: "time without time zone");
      AddType("interval", DbType.Object, typeof(TimeSpan), NpgsqlDbType.Interval, isDefault: false);

      // Binaries
      // Note: Postgres has special way of presenting binary blobs, so we provide special BytesToLiteral converter
      var binTypes = new Type[] { typeof(Vita.Common.Binary) };
      AddType("bytea", DbType.Binary, typeof(byte[]), NpgsqlDbType.Bytea, supportsMemo: true, clrTypes: binTypes, valueToLiteral: BytesToLiteral);
      AddType("array", DbType.Binary, typeof(byte[]), NpgsqlDbType.Array, isDefault: false, clrTypes: binTypes, valueToLiteral: BytesToLiteral);

      // Guid 
      AddType("uuid", DbType.Guid, typeof(Guid), NpgsqlDbType.Uuid);

    }

    public override VendorDbTypeInfo FindVendorDbTypeInfo(DbType dbType, Type clrType, bool isMemo) {
      var pgDbType = dbType; 
      switch(dbType) {
        case DbType.AnsiString: pgDbType = DbType.String; break;
        case DbType.AnsiStringFixedLength: pgDbType = DbType.StringFixedLength; break;           
      }
      return base.FindVendorDbTypeInfo(pgDbType, clrType, isMemo);
    }


    private static string BytesToLiteral(DbTypeInfo typeInfo, object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var bytes = (byte[])value;
      return @"E'\\x" + HexUtil.ByteArrayToHex(bytes) + "'";
    }

    private static string NumToLiteralWithCast(DbTypeInfo typeInfo, object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var strValue = value.ToString();
      return string.Format("CAST({0} AS {1})", strValue, typeInfo.SqlTypeSpec);
    }

    public override string GetDefaultColumnInitExpression(Type type) {
      if(type == typeof(bool))
        return "false";
      return base.GetDefaultColumnInitExpression(type);
    }
  }
}
