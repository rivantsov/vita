using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Data.Model;
using System.Globalization;
using Vita.Entities.Logging;

namespace Vita.Data.SQLite {

  public class SQLiteTypeRegistry : DbTypeRegistry {
    public static string DateTimeFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss.fffffffZ";

    public SQLiteTypeRegistry(SQLiteDbDriver driver) : base(driver) {
      var realTypes = new Type[] {typeof(Single), typeof(double), typeof(decimal)};
      var stringTypes = new Type[] { typeof(string), typeof(char), /*typeof(DateTime), typeof(TimeSpan)*/  };
      var blobTypes = new Type[] { typeof(Guid), typeof(Vita.Common.Binary), typeof(byte[]) };
      var intTypes = new Type[] {
        typeof(byte), typeof(sbyte), typeof(Int16), typeof(UInt16), typeof(Int32), typeof(UInt32), 
      };
      var int64Types = new Type[] { typeof(Int64), typeof(UInt64) };

      // Note: we associate multiple int types with single storage class, but intercept in particular cases 
      // (in GetDbTypeInfo below), and provide specific DbType values and converters 
      // We have to put Int32 into a separate class for enum types to work
      AddType("INT", DbType.Int32, typeof(Int32), clrTypes: intTypes, dbFirstClrType: typeof(Int32), aliases: "integer");
      AddType("INT64", DbType.Int64, typeof(Int64), clrTypes: int64Types, dbFirstClrType: typeof(Int64));
      AddType("REAL", DbType.Double, typeof(double), clrTypes: realTypes, dbFirstClrType: typeof(double));
      AddType("TEXT", DbType.String, typeof(string), supportsMemo: true, clrTypes: stringTypes, dbFirstClrType: typeof(string));
      AddType("BLOB", DbType.Binary, typeof(byte[]), supportsMemo: true, clrTypes: blobTypes,
          dbFirstClrType: typeof(Vita.Common.Binary), valueToLiteral: ConvertBinaryToLiteral);
      AddType("Date", DbType.DateTime, typeof(DateTime)); 
      AddType("Time", DbType.DateTimeOffset, typeof(TimeSpan));
      AddType("Bool", DbType.Boolean, typeof(bool));

      Converters.AddConverter<string, TimeSpan>(x => TimeSpan.Parse((string)x), x => ((TimeSpan)x).ToString("G"));
      Converters.AddConverter<Int64, bool>(x => (Int64)x == 1, x => (bool)x ? 1L : 0L);
      RegisterAutoMemoTypes("TEXT", "BLOB");

    }

    //Override base method to ignore DbType and isMemo - match only by ClrType
    public override VendorDbTypeInfo FindVendorDbTypeInfo(DbType dbType, Type clrType, bool isMemo) {
      var match = Types.FirstOrDefault(td => td.ClrTypes.Contains(clrType));
      return match;
    }

    public override DbTypeInfo GetDbTypeInfo(EntityMemberInfo member, SystemLog log) {
      var ti = base.GetDbTypeInfo(member, log);
      if(ti == null)
        return null;
      var type = member.DataType;
      if(type.IsNullableValueType())
        type = type.GetUnderlyingType();
      if (type.IsInt()) {
        ti.DbType = GetDbTypeForInt(type);
        // Assign converter for the specific target type
        var conv = new IntValueConverter() { TargetType = type };
        ti.ColumnToPropertyConverter = conv.ConvertValue;
        ti.PropertyToColumnConverter = DbValueConverter.NoConvertFunc;
      }
      return ti; 
    }

    private DbType GetDbTypeForInt(Type intType) {
      var tc = Type.GetTypeCode(intType);
      switch(tc) {
        case TypeCode.Byte: return DbType.Byte;
        case TypeCode.SByte: return DbType.SByte;
        case TypeCode.Int16: return DbType.Int16;
        case TypeCode.UInt16: return DbType.UInt16;
        case TypeCode.Int32: return DbType.Int32;
        case TypeCode.UInt32: return DbType.UInt32;
        case TypeCode.Int64: return DbType.Int64;
        case TypeCode.UInt64: return DbType.UInt64;
        case TypeCode.Boolean:
          return DbType.Boolean;
        case TypeCode.Char:
          return DbType.UInt16;
        default: return DbType.Int32;

      }

    }

    private static string ConvertBinaryToLiteral(DbTypeInfo typeInfo, object value) {
      var bytes = (byte[])value;
      return "x'" + HexUtil.ByteArrayToHex(bytes) + "'";
    }

    public static string DateTimeToString(DateTime dt) {
      var result = dt.ToString(DateTimeFormat);
      return result;
    }

    class IntValueConverter {
      public Type TargetType;
      public object ConvertValue(object value) {
        if(value == null || value == DBNull.Value)
          return value;
        if(value.GetType() == TargetType)
          return value;
        return Convert.ChangeType(value, TargetType);
      }
    }

  }//class
}
