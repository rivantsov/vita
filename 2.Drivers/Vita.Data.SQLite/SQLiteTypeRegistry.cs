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

namespace Vita.Data.SQLite {

  public class SQLiteTypeRegistry : DbTypeRegistry {
 
    public SQLiteTypeRegistry(SQLiteDbDriver driver) : base(driver) {
      var realTypes = new Type[] {typeof(Single), typeof(double), typeof(decimal)};
      var stringTypes = new Type[] { typeof(string), typeof(char), typeof(DateTime), typeof(TimeSpan) };
      var blobTypes = new Type[] { typeof(Guid), typeof(Vita.Common.Binary), typeof(byte[]) };
      var intTypes = new Type[] {
        typeof(byte), typeof(sbyte), typeof(Int16), typeof(UInt16), typeof(Int32), typeof(UInt32), 
        typeof(Int64), typeof(UInt64),
        typeof(bool), 
      }; 
      AddType("INTEGER", DbType.Int64, typeof(Int64), clrTypes: intTypes, dbFirstClrType: typeof(Int64), aliases: "int");
      AddType("REAL", DbType.Double, typeof(double), clrTypes: realTypes, dbFirstClrType: typeof(double));
      AddType("TEXT", DbType.String, typeof(string), supportsMemo: true, clrTypes: stringTypes, dbFirstClrType: typeof(string));
      AddType("BLOB", DbType.Binary, typeof(byte[]), supportsMemo: true, clrTypes: blobTypes, dbFirstClrType: typeof(Vita.Common.Binary), valueToLiteral: ConvertBinaryToLiteral);

      Converters.AddConverter<string, DateTime>(x => StringToDateTime(x), x => DateTimeToString(x));
      Converters.AddConverter<string, TimeSpan>(x => TimeSpan.Parse((string)x), x => ((TimeSpan)x).ToString("G"));
      Converters.AddConverter<Int64, bool>(x => (Int64)x == 1, x => (bool)x ? 1L : 0L);
      RegisterAutoMemoTypes("TEXT", "BLOB");
    }

    //Override base method to ignore DbType and isMemo - match only by ClrType
    public override VendorDbTypeInfo FindVendorDbTypeInfo(DbType dbType, Type clrType, bool isMemo) {
      var match = Types.FirstOrDefault(td => td.ClrTypes.Contains(clrType));
      return match;
    }

    private static string ConvertBinaryToLiteral(DbTypeInfo typeInfo, object value) {
      var bytes = (byte[])value;
      return "x'" + HexUtil.ByteArrayToHex(bytes) + "'";
    }

    protected object StringToDateTime(object x) {
      if(x == null || x == DBNull.Value)
        return DBNull.Value; 
      var str = x as string;
      if(string.IsNullOrWhiteSpace(str))
        return DBNull.Value;
      DateTime result;
      DateTime.TryParse(str, out result);
      return result;
    }

    // Prop -> DbCol
    protected object DateTimeToString(object x) {
      if(x == null || x == DBNull.Value)
        return DBNull.Value; 
      var dt = (DateTime)x;
      var result = dt.ToString("o"); //("yyyy-MM-DD HH-mm-ss.SSS");
      return result;
    }

  }//class


}
