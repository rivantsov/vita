using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data;
using Vita.Data.Driver; 

namespace Vita.Data.SqlCe {
  public class SqlCeTypeRegistry : DbTypeRegistry {
    public SqlCeTypeRegistry(SqlCeDbDriver driver) : base(driver) {
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
      AddType("nvarchar", DbType.String, typeof(string), SqlDbType.NVarChar, supportsMemo: false, args: "({size})");
      AddType("nchar", DbType.StringFixedLength, typeof(string), SqlDbType.NChar, args: "({size})", isDefault: false, clrTypes: new Type[] { typeof(char) });
      AddType("varchar", DbType.AnsiString, typeof(string), SqlDbType.VarChar, args: "({size})", isDefault: false);
      AddType("char", DbType.AnsiStringFixedLength, typeof(string), SqlDbType.Char, args: "({size})", isDefault: false);
      AddType("ntext", DbType.String, typeof(string), SqlDbType.NText, supportsMemo: true, isDefault: false);
      AddType("text", DbType.AnsiString, typeof(string), SqlDbType.Text, supportsMemo: true, isDefault: false);


      // Datetime
      AddType("datetime", DbType.DateTime, typeof(DateTime), SqlDbType.DateTime);
      //AddType("date", DbType.Date, typeof(DateTime), SqlDbType.Date, isDefault: false);
      //AddType("time", DbType.Time, typeof(TimeSpan), SqlDbType.Time);
      AddType("smalldatetime", DbType.DateTime, typeof(DateTime), SqlDbType.SmallDateTime, isDefault: false);

      // Binaries
      var binTypes = new Type[] { typeof(Vita.Common.Binary) };
      AddType("varbinary", DbType.Binary, typeof(byte[]), SqlDbType.VarBinary, supportsMemo: false, args: "({size})", clrTypes: binTypes);
      AddType("binary", DbType.Binary, typeof(byte[]), SqlDbType.Binary, isDefault: false, args: "({size})", clrTypes: binTypes);
      AddType("image", DbType.Binary, typeof(byte[]), SqlDbType.Image, supportsMemo: true, isDefault: false, clrTypes: binTypes);

      AddType("uniqueidentifier", DbType.Guid, typeof(Guid), SqlDbType.UniqueIdentifier);
      AddType("money", DbType.Currency, typeof(Decimal), SqlDbType.Money, isDefault: false);

      // DateTimeOffset - store it as string and provide converters
      AddType("nvarchar(40)", DbType.String, typeof(string), SqlDbType.NVarChar, isDefault: false, isSubType: true, clrTypes: new Type[] { typeof(DateTimeOffset) });
      Converters.AddConverter<string, DateTimeOffset>(StringToDateTimeOffset, DateTimeOffsetToString);
      AddType("nvarchar(20)", DbType.String, typeof(string), SqlDbType.NVarChar, isDefault: false, isSubType: true, clrTypes: new Type[] { typeof(TimeSpan) });
      Converters.AddConverter<string, TimeSpan>(x => TimeSpan.Parse((string)x), x => ((TimeSpan)x).ToString("G"));


      // the following types should be treated as auto-memo - whenevery property specifies this type explicitly through [Column(TypeName=?)] spec,
      // the column is set to Memo (unlimited size) - unless size is specified explicitly.
      RegisterAutoMemoTypes("image", "ntext", "text");
    }

    // The following two methods are used as read/write converters for SQL parameters
    //  Note: default ToString() representation of DatetimeOffset is local time (!) followed by offset.
    // To make values comparable in database, we use UtcTime in string representation, so we do our custom parsing
    public static object StringToDateTimeOffset(object value) {
      if(value == null || value == DBNull.Value) return DBNull.Value;
      var svalue = (string)value;
      if(string.IsNullOrWhiteSpace(svalue)) return DBNull.Value;
      var datePart = svalue.Substring(0, 27);
      var offsPart = svalue.Substring(28, 6);
      //Remove '+' if it's there - TimeSpan.Parse does not support +
      offsPart = offsPart.Replace("+", string.Empty);
      var utcDate = DateTime.ParseExact(datePart, "yyyy-MM-dd HH:mm:ss.fffffff", null);
      var offs = TimeSpan.Parse(offsPart);
      var localDate = utcDate.Add(offs);
      var result = new DateTimeOffset(localDate, offs); //expects local time
      return result;
    }
    public static object DateTimeOffsetToString(object value) {
      //Format: '"YYYY-MM-DD hh:mm:ss.nnnnnnn [+/-] hh:mm"'
      if(value == null || value == DBNull.Value)
        return null;
      DateTimeOffset dtOffset;
      if(value is DateTimeOffset?) {
        var temp = (DateTimeOffset?)value;
        if(temp == null)
          return null;
        dtOffset = temp.Value;
      } else if(value is DateTimeOffset)
        dtOffset = (DateTimeOffset)value;
      else {
        Util.Throw("DateTimeOffset converterter: invalid property value. Expected DateTimeOffset, found: {0} (type: {1})", value, value.GetType().Name);
        return null;
      }
      //Format the value
      var dtPart = dtOffset.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
      var offsPart = dtOffset.Offset.ToString(@"hh\:mm");
      var sign = (dtOffset.Offset.Hours >= 0) ? "+" : "-";
      var result = dtPart + " " + sign + offsPart;
      return result;
    }

  }
}
