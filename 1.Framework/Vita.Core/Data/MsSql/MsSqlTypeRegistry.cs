using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;

namespace Vita.Data.MsSql {
  public class MsSqlTypeRegistry : DbTypeRegistry {
    public VendorDbTypeInfo UserDefinedVendorDbType; 
    public MsSqlTypeRegistry(MsSqlDbDriver driver)  : base(driver) {

      //numerics
      AddType("real", DbType.Single, typeof(Single), SqlDbType.Real);
      AddType("float", DbType.Double, typeof(Double), SqlDbType.Float);
      AddType("decimal", DbType.Decimal, typeof(Decimal), SqlDbType.Decimal, args: "({precision},{scale})", aliases: "numeric");

      //integers
      // Note: for int types that do not have direct match in database type set, we assign bigger int type in database
      // For example, signed byte (CLR sbyte) does not have direct match, so we use 'smallint' to handle it in the database.
      // For ulong (UInt64) CLR type there's no bigger type, so we handle it using 'bigint' which is signed 64-bit int; as a result there might be overflows 
      // in some cases. 
      AddType("int", DbType.Int32, typeof(int), SqlDbType.Int, clrTypes: new Type[] { typeof(UInt16) });
      AddType("smallint", DbType.Int16, typeof(Int16), SqlDbType.SmallInt, clrTypes: new Type[] { typeof(sbyte) });
      AddType("tinyint", DbType.Byte, typeof(byte), SqlDbType.TinyInt);
      AddType("bigint", DbType.Int64, typeof(Int64), SqlDbType.BigInt, clrTypes: new Type[] { typeof(UInt32), typeof(UInt64) });

      // Bool
      AddType("bit", DbType.Boolean, typeof(bool), SqlDbType.Bit);
      // Strings
      AddType("nchar", DbType.StringFixedLength, typeof(string), SqlDbType.NChar, args: "({size})", isDefault: false, clrTypes: new Type[] { typeof(char) });
      AddType("varchar", DbType.AnsiString, typeof(string), SqlDbType.VarChar, args: "({size})", isDefault: false, memoArgs: "(max)");
      AddType("char", DbType.AnsiStringFixedLength, typeof(string), SqlDbType.Char, args: "({size})", isDefault: false);
      AddType("ntext", DbType.String, typeof(string), SqlDbType.NText, supportsMemo: true, isDefault: false);
      AddType("text", DbType.AnsiString, typeof(string), SqlDbType.Text, supportsMemo: true, isDefault: false);
      // just to constantly verify that it works correctly - we add default db type for 'String' last. Nevertheless, it should be picked up for strings by default, so order should not matter.
      AddType("nvarchar", DbType.String, typeof(string), SqlDbType.NVarChar, supportsMemo: true, args: "({size})", memoArgs: "(max)");


      // Datetime
      AddType("datetime2", DbType.DateTime2, typeof(DateTime), SqlDbType.DateTime2);
      AddType("date", DbType.Date, typeof(DateTime), SqlDbType.Date, isDefault: false);
      AddType("time", DbType.Time, typeof(TimeSpan), SqlDbType.Time);
      AddType("datetime", DbType.DateTime, typeof(DateTime), SqlDbType.DateTime, isDefault: false, valueToLiteral: DateTimeToLiteralMsSql);
      AddType("smalldatetime", DbType.DateTime, typeof(DateTime), SqlDbType.SmallDateTime, isDefault: false, 
                         valueToLiteral: DbValueToLiteralConverters.DateTimeToLiteralNoMs);

      // Binaries
      var binTypes = new Type[] { typeof(Vita.Common.Binary) };
      AddType("varbinary", DbType.Binary, typeof(byte[]), SqlDbType.VarBinary, supportsMemo: true, args: "({size})", memoArgs: "(max)", clrTypes: binTypes);
      AddType("binary", DbType.Binary, typeof(byte[]), SqlDbType.Binary, isDefault: false, args: "({size})", clrTypes: binTypes);
      AddType("image", DbType.Binary, typeof(byte[]), SqlDbType.Image, supportsMemo: true, isDefault: false, clrTypes: binTypes);

      AddType("uniqueidentifier", DbType.Guid, typeof(Guid), SqlDbType.UniqueIdentifier);
      AddType("DateTimeOffset", DbType.DateTimeOffset, typeof(DateTimeOffset), SqlDbType.DateTimeOffset);
      AddType("money", DbType.Currency, typeof(Decimal), SqlDbType.Money, isDefault: false);

      // MS SQL specific types. 
      // Note: DbType.Object is for "... general type representing any reference or value type not explicitly represented by another DbType value."
      // So we use DbType.Object for such cases
      AddType("smallmoney", DbType.Currency, typeof(Decimal), SqlDbType.SmallMoney, isDefault: false);
      AddType("hierarchyid", DbType.Object, typeof(byte[]), SqlDbType.Binary, isDefault: false, clrTypes: binTypes);
      AddType("geography", DbType.Object, typeof(byte[]), SqlDbType.Udt, isDefault: false, supportsMemo: true, clrTypes: binTypes);
      AddType("geometry", DbType.Object, typeof(byte[]), SqlDbType.Udt, isDefault: false, supportsMemo: true, clrTypes: binTypes);
      AddType("timestamp", DbType.Binary, typeof(byte[]), SqlDbType.Timestamp, isDefault: false, aliases: "rowversion");
      AddType("xml", DbType.Object, typeof(string), SqlDbType.Xml, isDefault: false, supportsMemo: true);
      AddType("sql_variant", DbType.Object, typeof(object), SqlDbType.Variant, isDefault: false, valueToLiteral: DbValueToLiteralConverters.DefaultValueToLiteral);

      // the following types should be treated as auto-memo - whenevery property specifies this type explicitly through [Column(TypeName=?)] spec,
      // the column is set to Unlimited (unlimited size) - unless size is specified explicitly.
      RegisterAutoMemoTypes("nvarchar(max)", "image", "ntext", "text", "geography", "geometry", "xml");


      UserDefinedVendorDbType = new VendorDbTypeInfo("UserDefined", DbType.Object, typeof(object), null, null, null, typeof(object), VendorDbTypeFlags.UserDefined, null, null, (int)SqlDbType.Structured);
    }
    // Special converters for literal presentations (used in batch mode). Default converter provides too much precision and it blows up 
    public static string DateTimeToLiteralMsSql(DbTypeInfo typeInfo, object value) {
      if(value == null || value == DBNull.Value)
        return "NULL";
      var dt = (DateTime)value;
      var str = "'" + dt.ToString("yyyy-MM-ddTHH:mm:ss.FFF") + "'";
      return str;
    }

  } //class
}
