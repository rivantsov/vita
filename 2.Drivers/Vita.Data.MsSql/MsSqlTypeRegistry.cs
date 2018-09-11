using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Server;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Data.MsSql {
  public class MsSqlTypeRegistry : DbTypeRegistry {
    // User-defined table type, created by VITA to be used to send array-type parameters to SQLs and stored procedures
    public static string ArrayAsTableTypeName = "Vita_ArrayAsTable";

    public DbStorageType ArrayAsTableTypeDef;


    public MsSqlTypeRegistry(MsSqlDbDriver driver) : base(driver) {
      var none = new Type[] { };
      //numerics
      AddTypeDef("real", SqlDbType.Real, DbType.Single, typeof(Single));
      AddTypeDef("float", SqlDbType.Float, DbType.Double, typeof(Double));
      AddTypeDef("decimal", SqlDbType.Decimal, DbType.Decimal, typeof(Decimal), args: "({0},{1})", aliases: "numeric");

      //integers
      // Note: for int types that do not have direct match in database type set, we assign bigger int type in database
      // For example, signed byte (CLR sbyte) does not have direct match, so we use 'smallint' to handle it in the database.
      // For ulong (UInt64) CLR type there's no bigger type, so we handle it using 'bigint' which is signed 64-bit int; as a result there might be overflows 
      // in some cases. 
      AddTypeDef("int", SqlDbType.Int, DbType.Int32, typeof(int), mapToTypes: new[] { typeof(Int32), typeof(UInt16) });
      AddTypeDef("smallint", SqlDbType.SmallInt, DbType.Int16, typeof(Int16), mapToTypes: new[] { typeof(Int16), typeof(sbyte) });
      AddTypeDef("tinyint", SqlDbType.TinyInt, DbType.Byte, typeof(byte));
      AddTypeDef("bigint", SqlDbType.BigInt, DbType.Int64, typeof(Int64), 
          mapToTypes: new Type[] { typeof(UInt32), typeof(Int64), typeof(UInt64) });

      // Bool
      AddTypeDef("bit", SqlDbType.Bit, DbType.Boolean, typeof(bool), valueToLiteral: BoolToBitLiteral);
      // Strings; nvarchar(...) maps to string; the rest are not mapped
      AddTypeDef("nvarchar", SqlDbType.NVarChar, DbType.String, typeof(string), args: "({0})");
      AddTypeDef("nvarchar(max)", SqlDbType.NVarChar, DbType.String, typeof(string), flags: DbTypeFlags.Unlimited, loadTypeName: "nvarchar");

      AddTypeDef("varchar", SqlDbType.VarChar, DbType.AnsiString, typeof(string), args: "({0})", mapToTypes: none);
      AddTypeDef("varchar(max)", SqlDbType.VarChar, DbType.AnsiString, typeof(string), flags: DbTypeFlags.Unlimited, 
          mapToTypes: none, loadTypeName: "varchar");
      AddTypeDef("nchar", SqlDbType.NChar, DbType.StringFixedLength, typeof(string), args: "({0})", mapToTypes: new[] { typeof(char) });
      AddTypeDef("char", SqlDbType.Char, DbType.AnsiStringFixedLength, typeof(string), args: "({0})", mapToTypes: none);
      //obsolete 
      AddTypeDef("ntext", SqlDbType.NText, DbType.String, typeof(string), DbTypeFlags.Unlimited | DbTypeFlags.ObsoleteType, mapToTypes: none);
      AddTypeDef("text", SqlDbType.Text, DbType.AnsiString, typeof(string), DbTypeFlags.Unlimited | DbTypeFlags.ObsoleteType, mapToTypes: none);

      // Datetime
      AddTypeDef("datetime2", SqlDbType.DateTime2, DbType.DateTime2, typeof(DateTime));
      AddTypeDef("date", SqlDbType.Date, DbType.Date, typeof(DateTime), mapToTypes: none);
      AddTypeDef("time", SqlDbType.Time, DbType.Time, typeof(TimeSpan), mapToTypes: new[] { typeof(TimeSpan) });
      AddTypeDef("datetime", SqlDbType.DateTime, DbType.DateTime, typeof(DateTime), valueToLiteral: DateTimeToLiteralMsSql, mapToTypes: none);
      AddTypeDef("smalldatetime", SqlDbType.SmallDateTime, DbType.DateTime, typeof(DateTime),
                         valueToLiteral: DbValueToLiteralConverters.DateTimeToLiteralNoMs, mapToTypes: none);

      // Binaries
      var binTypes = new Type[] { typeof(byte[]), typeof(Vita.Entities.Binary) };
      AddTypeDef("varbinary", SqlDbType.VarBinary, DbType.Binary, typeof(byte[]), DbTypeFlags.None, 
                              args: "({0})", mapToTypes: binTypes);
      AddTypeDef("varbinary(max)", SqlDbType.VarBinary, DbType.Binary, typeof(byte[]), DbTypeFlags.Unlimited,
                              mapToTypes: binTypes, loadTypeName: "varbinary");
      AddTypeDef("binary", SqlDbType.Binary, DbType.Binary, typeof(byte[]), args: "({0})", mapToTypes: none);
      AddTypeDef("image", SqlDbType.Image, DbType.Binary, typeof(byte[]), DbTypeFlags.Unlimited, mapToTypes: none);

      AddTypeDef("uniqueidentifier", SqlDbType.UniqueIdentifier, DbType.Guid, typeof(Guid) );
      AddTypeDef("datetimeoffset", SqlDbType.DateTimeOffset, DbType.DateTimeOffset, typeof(DateTimeOffset));
      AddTypeDef("money", SqlDbType.Money, DbType.Currency, typeof(Decimal), flags: DbTypeFlags.None, mapToTypes: none);

      // MS SQL specific types. 
      // Note: DbType.Object is for "... general type representing any reference or value type not explicitly represented by another DbType value."
      // So we use DbType.Object for such cases
      AddTypeDef("smallmoney", SqlDbType.SmallMoney, DbType.Currency, typeof(Decimal), mapToTypes: none);

      AddTypeDef("rowversion", SqlDbType.Timestamp, DbType.Binary, typeof(byte[]), aliases: "timestamp", mapToTypes: none);
      AddTypeDef("xml", SqlDbType.Xml, DbType.Object, typeof(string), flags: DbTypeFlags.Unlimited, mapToTypes: none);
      AddTypeDef("sql_variant", SqlDbType.Variant, DbType.Object, typeof(object), 
        valueToLiteral: DbValueToLiteralConverters.DefaultValueToLiteral, mapToTypes: none);

      // we register these exotic binary types as unlimited
      AddTypeDef("hierarchyid", SqlDbType.Binary, DbType.Object, typeof(byte[]), flags: DbTypeFlags.Unlimited, mapToTypes: none);
      // for geography, geometry SqlDbType.Udt does not work
      AddTypeDef("geography", SqlDbType.Binary, DbType.Object, typeof(byte[]), flags: DbTypeFlags.Unlimited, mapToTypes: none);
      AddTypeDef("geometry", SqlDbType.Binary, DbType.Object, typeof(byte[]), flags: DbTypeFlags.Unlimited, mapToTypes: none);


      //Init table type
      ArrayAsTableTypeDef = AddTypeDef(ArrayAsTableTypeName, SqlDbType.Structured, DbType.Object, columnOutType: typeof(object),
          mapToTypes: none,
          dbFirstClrType: typeof(object), flags: DbTypeFlags.UserDefined, valueToLiteral: base.GetListLiteral);
      ArrayAsTableTypeDef.ConvertToTargetType = ConvertListToRecordList;
      ArrayAsTableTypeDef.IsList = true;
    }

    public override DbStorageType FindDbTypeDef(string typeName, bool unlimited) {
      typeName = typeName.ToLowerInvariant(); 
      switch(typeName) {
        case "image":
        case "text":
        case "ntext":
        case "xml":
          unlimited = true;
          break;
      }
      return base.FindDbTypeDef(typeName, unlimited);
    }
    public override DbStorageType FindStorageType(Type clrType, bool isMemo) {
      var td = base.FindStorageType(clrType, isMemo);
      if(td != null)
        return td;
      if(clrType.IsListOfDbPrimitive())
        return ArrayAsTableTypeDef;
      return null; 
    }

    // Special converters for literal presentations (used in batch mode). Default converter provides too much precision and it blows up 
    public static string DateTimeToLiteralMsSql(object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var dt = (DateTime)value;
      var str = "'" + dt.ToString("yyyy-MM-ddTHH:mm:ss.FFF") + "'";
      return str;
    }

    public static string BoolToBitLiteral(object value) {
      if(value == null)
        return "NULL";
      var b = (bool)value;
      return b ? "1" : "0";
    }

    // Used for sending lists in SqlParameter, to use in SQL clauses like 'WHERE x IN (@P0)"
    // @P0 should be declarate as VITA_ArrayAsTable data type. 
    // We can use DataTable as a container, but DataTable is not supported by .NET core;
    // we use alternative: IEnumerable<SqlDataRecord>, it is supported. 
    // TODO: review the method and optimize it. 
    internal static object ConvertListToRecordList(object value) {
      var list = value as System.Collections.IEnumerable;
      var valueType = value.GetType();
      Util.Check(valueType.IsListOfDbPrimitive(out Type elemType),
        "Value must be list of DB primitives. Value type: {0} ", valueType);
      if(elemType.IsNullableValueType())
        elemType = Nullable.GetUnderlyingType(elemType);
      bool isEnum = elemType.IsEnum;
      var records = new List<SqlDataRecord>();
      var colData = new SqlMetaData("Value", SqlDbType.Variant);
      foreach(object v in list) {
        var rec = new SqlDataRecord(colData);
        var v1 = isEnum ? (int)v : v;
        rec.SetValue(0, v1);
        records.Add(rec);
      }
      if(records.Count == 0)
        return null; // with 0 rows throws error, advising to send NULL
      return records;
    }

  } //class
}
