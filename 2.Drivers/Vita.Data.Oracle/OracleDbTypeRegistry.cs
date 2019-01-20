using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq; 
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Driver.TypeSystem;

namespace Vita.Data.Oracle {

  public partial class OracleDbTypeRegistry : DbTypeRegistry {
    Driver.TypeSystem.DbTypeDef NumericTypeDef;
    DbTypeInfo _boolTypeInfo;
    DbTypeInfo _byteTypeInfo;
    DbTypeInfo _int16TypeInfo;
    DbTypeInfo _int32TypeInfo;
    DbTypeInfo _int64TypeInfo;


    public OracleDbTypeRegistry(OracleDbDriver driver) : base(driver) {

      var stNumber = NumericTypeDef = base.AddDbTypeDef("number", typeof(decimal), DbTypeFlags.PrecisionScale, 
        aliases: "numeric", toLiteral: OracleConverters.NumberToLiteral);
      //save mappings in fields - we will use them in one of the methods
      _int64TypeInfo = Map(typeof(Int64), stNumber, prec: 20);
      _int32TypeInfo = Map(typeof(Int32), stNumber, prec: 10);
      _int16TypeInfo = Map(typeof(Int16), stNumber, prec: 5);
      _byteTypeInfo = Map(typeof(byte), stNumber, prec: 3);
      Map(typeof(sbyte), stNumber, prec: 3);
      Map(typeof(UInt64), stNumber, prec: 20);
      Map(typeof(UInt32), stNumber, prec: 10);
      Map(typeof(UInt16), stNumber, prec: 5);
      // bools are stored as number(1)
      _boolTypeInfo = Map(typeof(bool), stNumber, prec: 1);
      // number(1) is read as Int16 by Oracle data reader
      base.Converters.AddConverter<Int16, bool>(OracleConverters.IntToBool, x => (bool)x ? (Int16)1 : (Int16) 0);
      // LINQ needs bool<->decimal converter
      base.Converters.AddConverter<decimal, bool>(OracleConverters.DecimalToBool, x => (bool)x ? 1m : 0m);

      //floats
      AddDbTypeDef("binary_float", typeof(Single), aliases: "real");
      AddDbTypeDef("binary_double", typeof(Double), aliases: "double");

      // Strings; string is mapped to nvarchar(...); 
      var stNVarchar2 = base.AddDbTypeDef("nvarchar2", typeof(string), DbTypeFlags.Size, specialType: DbSpecialType.String);
      var stVarchar2 = base.AddDbTypeDef("varchar2", typeof(string), DbTypeFlags.Size | DbTypeFlags.Ansi, 
                                       specialType: DbSpecialType.StringAnsi);
      var stNChar = base.AddDbTypeDef("nchar", typeof(string), DbTypeFlags.Size);
      var stChar = base.AddDbTypeDef("char", typeof(string), DbTypeFlags.Size | DbTypeFlags.Ansi);
      var stNclob = base.AddDbTypeDef("nclob", typeof(string), DbTypeFlags.Unlimited, specialType: DbSpecialType.StringUnlimited);
      var stClob = base.AddDbTypeDef("clob", typeof(string), DbTypeFlags.Unlimited | DbTypeFlags.Ansi, 
                                     specialType: DbSpecialType.StringAnsiUnlimited);
      var stLong = base.AddDbTypeDef("long", typeof(string),
                   DbTypeFlags.Unlimited | DbTypeFlags.Obsolete | DbTypeFlags.Ansi);
      //char(1)
      Map(typeof(char), stNChar, 1);
      //binary
      var binInit = "hextoraw('00')";
      var tBinary = typeof(Vita.Entities.Binary);
      var stRaw = base.AddDbTypeDef("raw", typeof(byte[]), DbTypeFlags.Size, specialType: DbSpecialType.Binary,
                          toLiteral: OracleConverters.BytesToLiteral, columnInit: binInit);
      Map(tBinary, stRaw);
      // guid mapping
      Map(typeof(Guid), stRaw, size: 16);
      var stBlob = base.AddDbTypeDef("blob", typeof(byte[]), DbTypeFlags.Unlimited,
                    specialType: DbSpecialType.BinaryUnlimited, toLiteral: OracleConverters.BytesToLiteral, columnInit: binInit);
      Map(tBinary, stBlob);
      var stLongRaw = base.AddDbTypeDef("long raw", typeof(byte[]), DbTypeFlags.Size | DbTypeFlags.Unlimited | DbTypeFlags.Obsolete, 
                      toLiteral: OracleConverters.BytesToLiteral, columnInit: binInit);
      Map(tBinary, stLongRaw);
      var stRowId = base.AddDbTypeDef("rowid", typeof(byte[]), mapColumnType: false, toLiteral: OracleConverters.BytesToLiteral, columnInit: binInit);
      Map(tBinary, stRowId);

      // date
      var dateInit = OracleConverters.DateTimeToLiteral(new DateTime(1900, 1, 1));

      var stDate = AddDbTypeDef("date", typeof(DateTime), columnInit: dateInit, toLiteral: OracleConverters.DateTimeToLiteral);
      var stTimeStamp = AddDbTypeDef("timestamp", typeof(DateTime), 
                     toLiteral: OracleConverters.DateTimeToLiteral, aliases: "timestamp(6)");
      var stTimeStampTZ = AddDbTypeDef("timestamp with time zone", typeof(DateTime),
                     toLiteral: OracleConverters.DateTimeToLiteral);
      var stTimeStampLTZ = AddDbTypeDef("timestamp with local time zone", typeof(DateTime),
                     toLiteral: OracleConverters.DateTimeToLiteral);
      //intervals.
      // example type loaded from db: 'INTERVAL DAY(2) TO SECOND(6)'
      var stIntervalDayToSecond = base.AddDbTypeDef(PrefixIntervalDay, typeof(TimeSpan), DbTypeFlags.Precision | DbTypeFlags.Scale,
           specTemplate: PrefixIntervalDay + "({0}) to second ({1})", 
           toLiteral: OracleConverters.TimeSpanToLiteral);
      Map(typeof(TimeSpan), stIntervalDayToSecond, prec: 3, scale: 3);

      var stIntervalYearToMonth = base.AddDbTypeDef(PrefixIntervalYear, typeof(TimeSpan), DbTypeFlags.Precision | DbTypeFlags.Scale,
           specTemplate: PrefixIntervalDay + "({0}) to month ({1})",
           toLiteral: OracleConverters.TimeSpanToLiteral);

      
    }// constructor

    public const string PrefixInterval = "interval";
    public const string PrefixIntervalDay = "interval day";
    public const string PrefixIntervalYear = "interval year";

    public override DbTypeInfo GetDbTypeInfo(string typeName, long size = 0, byte prec = 0, byte scale = 0) {
      typeName = typeName.ToLowerInvariant();
      // Special cases for Oracle
      // numeric -> int types
      if(typeName == "number" && scale == 0) {
        if(prec == 1)
          return _boolTypeInfo;
        if(prec <= 3)
          return _byteTypeInfo;
        if(prec <= 5)
          return _int16TypeInfo;
        if(prec <= 10)
          return _int32TypeInfo;
        if(prec <= 20)
          return _int64TypeInfo;
      } //if number

      if(typeName.StartsWith(PrefixInterval)) {
        if(typeName.StartsWith(PrefixIntervalDay))
          typeName = PrefixIntervalDay;
        else if(typeName.StartsWith(PrefixIntervalYear))
          typeName = PrefixIntervalYear;
      }
      return base.GetDbTypeInfo(typeName, size, prec, scale);
    }

    public override DbValueConverter GetDbValueConverter(DbTypeInfo typeInfo, Type memberType) {
      return base.GetDbValueConverter(typeInfo, memberType);
    }

    protected override DbTypeInfo CreateDbTypeInfo(Type clrType, DbTypeDef typeDef, long size = 0, byte prec = 0, byte scale = 0) {
      var mapping = base.CreateDbTypeInfo(clrType, typeDef, size, prec, scale);
      // Big trouble - Oracle provider returns different value types in numeric columns with scale ==0 (integers)
      // depending on precision value 
      if (typeDef == NumericTypeDef) {
        if (mapping.Scale == 0) {
          if(mapping.Precision <= 4) {
            mapping.ColumnOutType = typeof(Int16);
            mapping.ColumnReader = (rec, i) => rec.GetInt16(i);
          } else if(mapping.Precision <= 9) {
            mapping.ColumnOutType = typeof(Int32); 
            mapping.ColumnReader = (rec, i) => rec.GetInt32(i);
          } else if(mapping.Precision <= 18) {
            mapping.ColumnOutType = typeof(Int64); 
            mapping.ColumnReader = (rec, i) => rec.GetInt64(i);
          } else {
            mapping.ColumnOutType = typeof(decimal); 
            mapping.ColumnReader = (rec, i) => rec.GetDecimal(i);
          }
        } // if mapping.Scale == 0
      } // if storageType ==
      return mapping; 
    }

    public override string FormatTypeSpec(DbTypeDef typeDef, long size = 0, byte prec = 0, byte scale = 0) {
      if(typeDef.Name.StartsWith(PrefixIntervalDay))
        return $"interval day({prec}) to second({scale})";
      if(typeDef.Name.StartsWith(PrefixIntervalDay))
        return $"interval year({prec}) to month({scale})";
      return base.FormatTypeSpec(typeDef, size, prec, scale);
    }
  } //class

}
