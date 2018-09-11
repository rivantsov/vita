using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Vita.Data.Driver {

  public class DbColumnTypeInfo {
    static Func<object, object> _noConvPropToColumn = x => x ?? DBNull.Value;
    static Func<object, object> _noConvColToProp = x => x;

    public DbStorageType StorageType;
    public string SqlTypeSpec;
    public bool IsNullable;
    public long Size;
    public byte Scale;
    public byte Precision; 
    public string InitExpression; //value to initialize nullable column when it switches to non-nullable; this string will be put directly into update SQL
    //Value converters
    public Func<object, object> ColumnToPropertyConverter = _noConvColToProp;
    public Func<object, object> PropertyToColumnConverter = _noConvPropToColumn;
    public Func<object, string> ToLiteral;

    public DbColumnTypeInfo(DbStorageType typeDef, string sqlTypeSpec, bool isNullable,
                         long size = 0, byte precision = 0, byte scale = 0, string initExpression = null) {
      StorageType = typeDef;
      SqlTypeSpec = sqlTypeSpec;
      IsNullable = isNullable;
      Size = size;
      Precision = precision;
      Scale = scale;
      InitExpression = initExpression;
      ToLiteral = ToLiteralDefaultImpl;
    }

    private string ToLiteralDefaultImpl(object value) {
      var convValue = PropertyToColumnConverter(value);
      return StorageType.ValueToLiteral(convValue);
    }

    public override string ToString() {
      return SqlTypeSpec;
    }

    public static object[] GetTypeArgs(long size, int prec, int scale) {
      if(prec != 0 || scale != 0)
        return new object[] { prec, scale };
      else if(size != 0)
        return new object[] { (int)size };
      else
        return _emptyArray;
    }
    static object[] _emptyArray = new object[] { }; 
  }//class


}
