using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Entities;

namespace Vita.Data.Driver {

  using ConvertFunc = Func<object, object>;

  public class DbValueConverter {
    public static ConvertFunc NoConvertFunc = x => x;

    public readonly Type ColumnType;   //The CLR type of value returned by DB
    public readonly Type PropertyType; // The type of property in entity
    public ConvertFunc ColumnToPropertyUnsafe; // FromType -> ToType
    public ConvertFunc PropertyToColumnUnsafe;
    //Safe, wrapped versions
    public ConvertFunc ColumnToProperty { get; set; } // FromType -> ToType
    public ConvertFunc PropertyToColumn { get; set; }

    public DbValueConverter(Type columnType, Type propertyType, ConvertFunc columnToProperty, ConvertFunc propertyToColumn) {
      ColumnType = columnType;
      PropertyType = propertyType;
      ColumnToPropertyUnsafe = columnToProperty ?? NoConvertFunc;
      PropertyToColumn = propertyToColumn ?? NoConvertFunc;
      ColumnToProperty = ConvertSafeColumnToProp;      
    }

    private object ConvertSafeColumnToProp(object value) {
      try {
        if (value == DBNull.Value || value == null)
          return DBNull.Value;
        return ColumnToPropertyUnsafe(value);
      } catch (Exception ex) {
        //value is not null here, garanteed
        var msg = Util.SafeFormat(
          "Failed to convert column value '{0}', type {1}. Converter for column type {2}, property type {3}.", 
          value, value.GetType(), ColumnType, PropertyType);
        throw new Exception(msg, ex); 
      }
    }
    private object ConvertSafePropToColumn(object value) {
      try {
        if (value == DBNull.Value || value == null)
          return DBNull.Value;
        else
          return PropertyToColumnUnsafe(value);
      } catch (Exception ex) {
        //value is not null here, garanteed
        var msg = Util.SafeFormat(
          "Failed to convert property value '{0}', type {1}. Converter for column type {2}, property type {3}.",
          value, value.GetType(), ColumnType, PropertyType);
        throw new Exception(msg, ex);
      }
    }

    public override string ToString() {
      return ColumnType + "->" + PropertyType;
    }

    public static DbValueConverter NoConvert = new DbValueConverter(typeof(object), typeof(object), NoConvertFunc, NoConvertFunc);

  }

  public class DbValueConverterRegistryNew {

    #region nested classes
    public class TypeTuple : IEquatable<TypeTuple> {
      public readonly Type X;
      public readonly Type Y;
      int _hashCode;
      public TypeTuple(Type x, Type y) {
        X = x;
        Y = y;
        _hashCode = x.GetHashCode() << 1 + y.GetHashCode();
      }
      public override int GetHashCode() {
        return _hashCode;
      }

      public bool Equals(TypeTuple other) {
        return other.X == X && other.Y == Y;
      }
      public override bool Equals(object obj) {
        return Equals((TypeTuple)obj);
      }
    }

    #endregion

    IDictionary<TypeTuple, DbValueConverter> _converters = new ConcurrentDictionary<TypeTuple, DbValueConverter>();

    public DbValueConverterRegistryNew() {
      BuildDefaultConverters(typeof(byte), typeof(sbyte), typeof(Int16), typeof(UInt16), typeof(Int32), typeof(UInt32),
        typeof(Int64), typeof(UInt64));
      BuildDefaultConverters(typeof(Single), typeof(double), typeof(decimal));
      AddConverter<byte[], Binary>(ByteArrayToBinary, BinaryToByteArray);
      AddConverter<byte[], Guid>(ByteArrayToGuid, GuidToByteArray);
      AddConverter<string, char>(x => x == null ? '\0' : ((string)x)[0], x => ((char)x).ToString());
      // decimal -> int
      BuildDefaultConverters(typeof(Int64), typeof(UInt64), typeof(Int32), typeof(UInt32),
          typeof(Int16), typeof(UInt16), typeof(byte), typeof(sbyte),
          typeof(decimal));
    }

    public DbValueConverter AddConverter<TColumn, TProperty>(ConvertFunc columnToProperty, ConvertFunc propertyToColumn) {
      return AddConverter(typeof(TColumn), typeof(TProperty), columnToProperty, propertyToColumn);
    }

    public DbValueConverter AddConverter(Type columnType, Type propertyType, ConvertFunc columnToProperty, ConvertFunc propertyToColumn) {
      var conv = new DbValueConverter(columnType, propertyType, columnToProperty, propertyToColumn);
      AddConverter(conv);
      return conv; 
    }

    public DbValueConverter GetConverter(Type columnType, Type memberType) {
      // do not use one no-convert instance; conv should have Type properties set
      if(columnType == memberType)
        return DbValueConverter.NoConvert;
      var types = new TypeTuple(columnType, memberType);
      DbValueConverter conv;
      if(_converters.TryGetValue(types, out conv))
        return conv; 
      //if it is enum or nullable, create converter on the fly
      if (memberType.IsNullableValueType()) {
        var toTypeBase = Nullable.GetUnderlyingType(memberType);
        if (toTypeBase == columnType) {
          //Build T->Nullable<T> converter
          conv = BuildNullableValueConverter(memberType);
          AddConverter(conv);
          return conv; 
        }
        //Otherwise try to get base converter
        var baseConv = GetConverter(columnType, toTypeBase);
        if (baseConv == null)
          return null;
        //Build combined converter
        var nullConv = BuildNullableValueConverter(memberType, baseConv);
        AddConverter(nullConv); 
        return nullConv;
      } //if IsNullableValueType
      if (memberType.IsEnum) {
        conv = BuildEnumValueConverter(columnType, memberType);
        AddConverter(conv);
        return conv; 
      }
      return null;
    }//method


    private void AddConverter(DbValueConverter converter) {
      var types = new TypeTuple(converter.ColumnType, converter.PropertyType);
      _converters[types] = converter;
    }

    #region some standard converters
    public static Byte[] BinaryToByteArray(object value) {
      if(value == null || value == DBNull.Value)
        return null;
      if(value is byte[])
        return (byte[])value;
      var b = value as Binary;
      if(b != null)
        return b.ToArray();
      Util.Throw("Invalid binary value ({0}), expected byte[] or Binary.", value.GetType());
      return null;
    }
    public static Binary ByteArrayToBinary(object value) {
      if(value == null || value == DBNull.Value)
        return null;
      if(value is byte[])
        return new Binary((byte[])value); //, makeCopy: false); //do not make copy of the data
      var b = value as Binary;
      if(b != null)
        return b;
      Util.Throw("Invalid binary value ({0}), expected byte[] or Binary.", value.GetType());
      return null;
    }

    public static object ByteArrayToGuid(object value) {
      if(value == DBNull.Value) return DBNull.Value;
      // It might happen that driver performs conversion to Guid automatically for binary(16) columns
      if(value is Guid)
        return (Guid)value;
      var bytes = (byte[])value;
      var guid = new Guid(bytes);
      return guid;
    }

    public static object GuidToByteArray(object value) {
      if(value == null || value == DBNull.Value)
        return DBNull.Value;
      var guid = (Guid)value;
      var bytes = guid.ToByteArray();
      return bytes;
    }
    #endregion 

    #region auto-converters between compatible types
    protected void BuildDefaultConverters(params Type[] types) {
      foreach(var t1 in types)
        foreach(var t2 in types)
          if(t1 != t2)
            AddDefaultConverter(t1, t2); 
    }
    protected DbValueConverter AddDefaultConverter(Type fromType, Type toType) {
      var conv = new DbValueConverter(fromType, toType, BuildImplicitConverter(fromType, toType), BuildImplicitConverter(toType, fromType));
      AddConverter(conv);
      //Test
      var fromV = Activator.CreateInstance(fromType);
      var toV = conv.ColumnToProperty(fromV);
      fromV = conv.PropertyToColumn(toV); 
      return conv; 
    }
    private Func<object, object> BuildImplicitConverter(Type fromType, Type toType) {
      var prm = Expression.Parameter(typeof(object));
      var fromValue = Expression.Convert(prm, fromType); //Unbox
      var toValue = Expression.Convert(fromValue, toType); 
      var bodyExpr = Expression.Convert(toValue, typeof(object)); //Box
      var funcExpr = Expression.Lambda(bodyExpr, prm);
      var func = funcExpr.Compile();
      return (Func<object, object>)func; 
    }

    #endregion

    #region Building derived converters - for nullable types and enums
    private DbValueConverter BuildNullableValueConverter(Type nullablePropertyType, DbValueConverter baseConv = null) {
      var valueFromNullable = BuildNullableGetValueFunc(nullablePropertyType);
      Type colType;
      ConvertFunc propToColFunc;
      ConvertFunc colToPropFunc; 
      if (baseConv == null) {
        colType = baseConv == null ? nullablePropertyType.GetUnderlyingStorageClrType() : baseConv.ColumnType;
        propToColFunc =
          x => x == null || x == DBNull.Value ?
                       DBNull.Value :
                       x.GetType().IsValueType ? x : valueFromNullable(x);
        colToPropFunc = x => x; 
      } else {
        colType = nullablePropertyType.GetUnderlyingStorageClrType();
        propToColFunc =
          x => x == null || x == DBNull.Value ?
                     DBNull.Value :
                     x.GetType().IsValueType ? baseConv.PropertyToColumn(x) :
                                               baseConv.PropertyToColumn(valueFromNullable(x));
        colToPropFunc = baseConv.ColumnToProperty;
      }
      var convObj = new DbValueConverter(colType,  nullablePropertyType, colToPropFunc, propToColFunc);
      return convObj;
    }
    private DbValueConverter BuildEnumValueConverter(Type columnType, Type enumType) {
      var colToEnum = BuildIntToEnumFunc(columnType, enumType);
      var enumToCol = BuildEnumToIntFunc(columnType, enumType);
      var convObj = new DbValueConverter(columnType, enumType, (ConvertFunc)colToEnum, (ConvertFunc)enumToCol);
      return convObj;
    }

    private Func<object, object> BuildNullableGetValueFunc(Type nullableType) {
      var prm = Expression.Parameter(typeof(object));
      var typedPrm = Expression.Convert(prm, nullableType);
      var valueProp = nullableType.GetProperty("Value");
      var readPropExpr = Expression.Property(typedPrm, "Value");
      var bodyExpr = Expression.Convert(readPropExpr, typeof(object));
      var funcExpr = Expression.Lambda(bodyExpr, prm);
      var func = funcExpr.Compile();
      return (Func<object, object>)func;
    }

    public static Func<object, object> BuildIntToEnumFunc(Type intType, Type enumType) {
      var baseEnumType = Enum.GetUnderlyingType(enumType); 
      var prm = Expression.Parameter(typeof(object));
      // var unboxed = Expression.Unbox(prm, intType);
      var intValue = Expression.Convert(prm, intType); // !! this fails, for SQLite incoming value is int64
      var enumIntValue = intValue; 
      if(baseEnumType != intType) 
        enumIntValue = Expression.Convert(intValue, baseEnumType);
      var enumValue = Expression.Convert(enumIntValue, enumType);
      var bodyExpr = Expression.Convert(enumValue, typeof(object));
      var funcExpr = Expression.Lambda(bodyExpr, prm);
      var func = funcExpr.Compile();
      return (Func<object, object>)func; 
    }

    public static Func<object, object> BuildEnumToIntFunc(Type intType, Type enumType) {
      var baseEnumType = Enum.GetUnderlyingType(enumType);
      var prm = Expression.Parameter(typeof(object));
      var enumValue = Expression.Convert(prm, enumType);
      var enumIntValue = Expression.Convert(enumValue, baseEnumType);
      var intValue = enumIntValue;
      if(baseEnumType != intType)
        intValue = Expression.Convert(enumIntValue, intType);
      var bodyExpr = Expression.Convert(intValue, typeof(object));
      var funcExpr = Expression.Lambda(bodyExpr, prm);
      var func = funcExpr.Compile();
      return (Func<object, object>)func;
    }

    #endregion 

  }

}
