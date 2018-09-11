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

namespace Vita.Data.Postgres {
  public class PgTypeRegistry : DbTypeRegistry {
    public PgTypeRegistry(PgDbDriver driver)  : base(driver) {
      var none = new Type[] { };

      AddTypeDef("real", NpgsqlDbType.Real, DbType.Single, typeof(Single), aliases: "float4");
      AddTypeDef("double precision", NpgsqlDbType.Double, DbType.Double, typeof(Double), aliases: "float8");
      AddTypeDef("numeric", NpgsqlDbType.Numeric, DbType.Decimal, typeof(Decimal), args: "({0},{1})");
      //Just to handle props marked with DbType=DbType.Currency
      AddTypeDef("money", NpgsqlDbType.Money, DbType.Currency, typeof(Decimal), mapToTypes: none,
            valueToLiteral: CreateFuncNumToLiteralWithCast("money"));

      //integers
      // Note: We have to provide explicit cast for certain int and currency types; otherwise, function overload resolution does not match the intended function 
      // (it works by function name and arg types match). Quite annoying. 
      AddTypeDef("int", NpgsqlDbType.Integer, DbType.Int32, typeof(int), aliases: "integer,oid,int4", 
                             mapToTypes: new Type[] { typeof(Int32), typeof(UInt16) });
      AddTypeDef("smallint", NpgsqlDbType.Smallint, DbType.Int16, typeof(Int16),  
                             mapToTypes: new Type[] { typeof(Int16), typeof(byte), typeof(sbyte) }, 
                             valueToLiteral: CreateFuncNumToLiteralWithCast("smallint"));
      AddTypeDef("bigint", NpgsqlDbType.Bigint,  DbType.Int64, typeof(Int64),  aliases: "int8",
                             mapToTypes: new Type[] { typeof(Int64), typeof(UInt32), typeof(UInt64) }, 
                             valueToLiteral: CreateFuncNumToLiteralWithCast("bigint"));

      // Bool
      AddTypeDef("boolean", NpgsqlDbType.Boolean, DbType.Boolean, typeof(bool));
      //Bit
      //TODO: finish Bit data types : http://www.postgresql.org/docs/8.4/static/datatype-bit.html

      // Strings
      AddTypeDef("character varying", NpgsqlDbType.Varchar, DbType.String, typeof(string),  args: "({0})", aliases: "varchar");
      AddTypeDef("character", NpgsqlDbType.Char, DbType.StringFixedLength, typeof(string),  args: "({0})",
                          mapToTypes: new Type[] { typeof(char) }, aliases: "char");
      AddTypeDef("text", NpgsqlDbType.Text, DbType.String, typeof(string), flags: DbTypeFlags.Unlimited);
      // Datetime
      AddTypeDef("timestamp without time zone", NpgsqlDbType.Timestamp, DbType.DateTime, typeof(DateTime));
      AddTypeDef("timestamp with time zone", NpgsqlDbType.TimestampTz,  DbType.Date, typeof(DateTimeOffset));
      AddTypeDef("date without time zone", NpgsqlDbType.Date, DbType.Date, typeof(DateTime), mapToTypes: none);
      AddTypeDef("time without time zone", NpgsqlDbType.Time, DbType.Time, typeof(TimeSpan));
      AddTypeDef("interval", NpgsqlDbType.Interval, DbType.Object, typeof(TimeSpan), mapToTypes: none);

      // Binaries
      // Note: Postgres has special way of presenting binary blobs, so we provide special BytesToLiteral converter
      var binTypes = new Type[] { typeof(byte[]), typeof(Vita.Entities.Binary) };
      AddTypeDef("bytea", NpgsqlDbType.Bytea, DbType.Binary, typeof(byte[]), 
                    mapToTypes: binTypes, valueToLiteral: BytesToLiteral);
      AddTypeDef("bytea", NpgsqlDbType.Bytea, DbType.Binary, typeof(byte[]), flags: DbTypeFlags.Unlimited,
                    mapToTypes: binTypes, valueToLiteral: BytesToLiteral);
      // AddTypeDef("array", NpgsqlDbType.Array, DbType.Binary, typeof(byte[]), mapToTypes: binTypes, flags: DbTypeFlags.Unlimited,
         //              valueToLiteral: BytesToLiteral);

      // Guid 
      AddTypeDef("uuid", NpgsqlDbType.Uuid, DbType.Guid, typeof(Guid));
    }

    public override DbStorageType FindDbTypeDef(DbType dbType, Type clrType, bool isMemo) {
      var pgDbType = dbType; 
      switch(dbType) {
        case DbType.AnsiString: pgDbType = DbType.String; break;
        case DbType.AnsiStringFixedLength: pgDbType = DbType.StringFixedLength; break;           
      }
      return base.FindDbTypeDef(pgDbType, clrType, isMemo);
    }

    public override DbStorageType FindStorageType(Type clrType, bool isMemo) {
      var typeDef = base.FindStorageType(clrType, isMemo);
      if(typeDef != null)
        return typeDef; 
      if (clrType.IsListOfDbPrimitive(out Type elemType)) {
        typeDef = ConstructArrayTypeDef(elemType);
      }
      return typeDef; 
    }


    private static string BytesToLiteral(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var bytes = (byte[])value;
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

    public override string GetEmptyListLiteral(DbStorageType elemTypeDef) {
      var pgDbType = (NpgsqlDbType) elemTypeDef.CustomDbType;
      var emptyList = string.Format("SELECT CAST(NULL AS {0}) WHERE 1=0", pgDbType);
      return emptyList;
    }

    private DbStorageType ConstructArrayTypeDef(Type elemType) {
      var elemTypeDef = this.FindStorageType(elemType, false);
      if(elemTypeDef == null)
        return null;
      //array
      var objArrType = typeof(object[]);
      var arrTypeDef = new DbStorageType(this, "array", DbType.Object, objArrType, null, mapToTypes: new[] { objArrType },
        dbFirstClrType: objArrType, flags: DbTypeFlags.Array, columnInit: null, loadTypeName: "array",
        aliases: "object[]", valueToLiteral: GetListLiteral, customDbType: (int)NpgsqlDbType.Array | elemTypeDef.CustomDbType);
      arrTypeDef.ConvertToTargetType = x => x;
      arrTypeDef.IsList = true;
      return arrTypeDef;
    }

  }
}
