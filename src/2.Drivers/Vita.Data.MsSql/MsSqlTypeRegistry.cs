using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Server;
using Vita.Data.Driver;
using Vita.Data.Driver.TypeSystem;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Data.MsSql {
  public class MsSqlTypeRegistry : DbTypeRegistry {

    public MsSqlTypeRegistry(MsSqlDbDriver driver) : base(driver) {
      var none = new Type[] { };
      //numerics
      AddDbTypeDef("real", typeof(Single));
      AddDbTypeDef("float", typeof(Double));
      var tsDec = base.AddDbTypeDef("decimal", typeof(Decimal), Data.Driver.TypeSystem.DbTypeFlags.Precision | Data.Driver.TypeSystem.DbTypeFlags.Scale, aliases: "numeric");
      // we do not have default mapping for decimal; but we set it to (18,4) if not provided

      //integers
      // Note: for int types that do not have direct match in database type set, we assign bigger int type in database
      // For example, signed byte (CLR sbyte) does not have direct match, so we use 'smallint' to handle it in the database.
      // For ulong (UInt64) CLR type there's no bigger type, so we handle it using 'bigint' which is signed 64-bit int; as a result there might be overflows 
      // in some cases. 
      var tsInt = AddDbTypeDef("int", typeof(int));
      MapMany(new[] { typeof(Int32), typeof(UInt16) }, tsInt);
      var tsSmallInt = AddDbTypeDef("smallint", typeof(Int16));
      Map(typeof(sbyte), tsSmallInt);
      AddDbTypeDef("tinyint", typeof(byte));
      var tsBigInt = AddDbTypeDef("bigint", typeof(Int64));
      MapMany(new Type[] { typeof(UInt32), typeof(UInt64) }, tsBigInt);

      // Bool
      AddDbTypeDef("bit", typeof(bool), toLiteral: BoolToBitLiteral);
      base.AddDbTypeDef("nvarchar", typeof(string), Data.Driver.TypeSystem.DbTypeFlags.Size, specialType: DbSpecialType.String);
      base.AddDbTypeDef("nvarchar(max)", typeof(string), flags: Data.Driver.TypeSystem.DbTypeFlags.Unlimited, specialType: DbSpecialType.StringUnlimited); 

      base.AddDbTypeDef("varchar", typeof(string), Data.Driver.TypeSystem.DbTypeFlags.Size | Data.Driver.TypeSystem.DbTypeFlags.Ansi, specialType: DbSpecialType.StringAnsi);
      base.AddDbTypeDef("varchar(max)", typeof(string), flags: Data.Driver.TypeSystem.DbTypeFlags.Unlimited | Data.Driver.TypeSystem.DbTypeFlags.Ansi, 
                                specialType: DbSpecialType.StringAnsiUnlimited); 
      var stNChar = base.AddDbTypeDef("nchar", typeof(string), Data.Driver.TypeSystem.DbTypeFlags.Size);
      Map(typeof(char), stNChar, size: 1);
      base.AddDbTypeDef("char", typeof(string), Data.Driver.TypeSystem.DbTypeFlags.Size | Data.Driver.TypeSystem.DbTypeFlags.Ansi);
      //obsolete 
      base.AddDbTypeDef("ntext", typeof(string), Data.Driver.TypeSystem.DbTypeFlags.Unlimited | Data.Driver.TypeSystem.DbTypeFlags.Obsolete);
      base.AddDbTypeDef("text", typeof(string), Data.Driver.TypeSystem.DbTypeFlags.Unlimited | Data.Driver.TypeSystem.DbTypeFlags.Obsolete);

      // Datetime
      // datetime2 may have optional #of-digits parameter (scale). Setting ArgsOptional makes allows it to register as default for 
      var datetime2 = base.AddDbTypeDef("datetime2", typeof(DateTime), Data.Driver.TypeSystem.DbTypeFlags.Precision, defaultPrecision: 7);

      base.AddDbTypeDef("datetime", typeof(DateTime), mapColumnType: false, toLiteral: DateTimeToLiteralMsSql);
      base.AddDbTypeDef("date", typeof(DateTime), mapColumnType: false);
      AddDbTypeDef("time", typeof(TimeSpan));
      base.AddDbTypeDef("smalldatetime", typeof(DateTime), mapColumnType: false,  toLiteral: DbValueToLiteralConverters.DateTimeToLiteralNoMs);

      // Binaries
      var tbinary = typeof(Vita.Entities.Binary);
      var stVarBin = base.AddDbTypeDef("varbinary", typeof(byte[]), Data.Driver.TypeSystem.DbTypeFlags.Size, specialType: DbSpecialType.Binary);
      Map(tbinary, stVarBin);
      var stVarBinMax = base.AddDbTypeDef("varbinary(max)", typeof(byte[]), Data.Driver.TypeSystem.DbTypeFlags.Unlimited, specialType: DbSpecialType.BinaryUnlimited);
      Map(tbinary, stVarBinMax);
      var tsBin = base.AddDbTypeDef("binary", typeof(byte[]), Data.Driver.TypeSystem.DbTypeFlags.Size);
      Map(tbinary, tsBin);
      base.AddDbTypeDef("image", typeof(byte[]), Data.Driver.TypeSystem.DbTypeFlags.Unlimited | Data.Driver.TypeSystem.DbTypeFlags.Obsolete);

      AddDbTypeDef("uniqueidentifier", typeof(Guid));

      // MS SQL specific types. 
      AddDbTypeDef("datetimeoffset", typeof(DateTimeOffset));
      base.AddDbTypeDef("money", typeof(Decimal), mapColumnType: false);
      base.AddDbTypeDef("smallmoney", typeof(Decimal), mapColumnType: false);

      base.AddDbTypeDef("rowversion", typeof(byte[]), aliases: "timestamp", mapColumnType: false);
      base.AddDbTypeDef("xml", typeof(string), flags: Data.Driver.TypeSystem.DbTypeFlags.Unlimited);
      AddDbTypeDef("sql_variant", typeof(object), toLiteral: DbValueToLiteralConverters.DefaultValueToLiteral);

      // we register these exotic binary types as unlimited
      base.AddDbTypeDef("hierarchyid", typeof(byte[]), flags: Data.Driver.TypeSystem.DbTypeFlags.Unlimited);
      // for geography, geometry SqlDbType.Udt does not work
      base.AddDbTypeDef("geography", typeof(byte[]), flags: Data.Driver.TypeSystem.DbTypeFlags.Unlimited);
      base.AddDbTypeDef("geometry", typeof(byte[]), flags: Data.Driver.TypeSystem.DbTypeFlags.Unlimited);

    }


    public override DbTypeInfo GetDbTypeInfo(string typeSpec, EntityMemberInfo forMember) {
      if (typeSpec.EndsWith("(max)", StringComparison.OrdinalIgnoreCase)) {
        if(!TypeDefsByName.TryGetValue(typeSpec, out var stype))
          return null;
        return stype.DefaultTypeInfo;
      }
      return base.GetDbTypeInfo(typeSpec, forMember);
    }

    public override DbTypeInfo GetDbTypeInfo(string typeName, long size = 0, byte prec = 0, byte scale = 0) {
      switch(typeName.ToLowerInvariant()) {
        case "varchar":
        case "nvarchar":
        case "varbinary":
          if(size < 0)
            typeName += "(max)";
          break; 
      }
      return base.GetDbTypeInfo(typeName, size, prec, scale);
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
    // @P0 should be declared as VITA_ArrayAsTable data type. 
    // The values are packed as IEnumerable<SqlDataRecord> 
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
