using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Entities.Logging;
using System.Collections;

namespace Vita.Data.Driver {
  //using DbValueToLiteralFunc = Func<DbTypeInfo, object, string>;
  using DbValueToLiteralFunc = Func<object, string>;
  using DbStorageTypeMap = Dictionary<Type, DbStorageType>;


  public class DbTypeRegistry {
    protected DbDriver Driver;

    public IList<DbStorageType> StorageTypesAll = new List<DbStorageType>();
    public DbStorageTypeMap StorageTypesLimited = new DbStorageTypeMap();
    public DbStorageTypeMap StorageTypesUnlimited = new DbStorageTypeMap(); 

    public DbValueConverterRegistry Converters = new DbValueConverterRegistry();

    public DbTypeRegistry(DbDriver driver) {
      Driver = driver;
    }

    public DbStorageType AddTypeDef<TCustomDbType>(string typeName, TCustomDbType customDbType,  DbType dbType, Type columnOutType, 
                                    DbTypeFlags flags = DbTypeFlags.None, string args = null,
                                    Type[] mapToTypes = null,
                                     Type dbFirstClrType = null, string aliases = null, string columnInit = null,
                                    DbValueToLiteralFunc valueToLiteral = null, string loadTypeName = null) 
                                    where TCustomDbType : Enum 
    {
      //validate that we have only one default type for clr type
      bool unlimited = flags.IsSet(DbTypeFlags.Unlimited);
      typeName = typeName.ToLowerInvariant();
      // If mapToTypes is skipped (null), we default to columnOutType; to specify to NOT map to any CLR types, 
      // mapToTypes should be set to empty array
      if(mapToTypes == null)
        mapToTypes = new[] { columnOutType };
      columnInit = columnInit ?? GetDefaultColumnInitExpression(columnOutType);
      var storageType = new DbStorageType(this, typeName, dbType, columnOutType, args, mapToTypes, dbFirstClrType,
                                         flags, aliases, valueToLiteral, (int)(object) customDbType, columnInit, loadTypeName);
      //register in lists/dictionaries
      var typeSet = unlimited ? this.StorageTypesUnlimited : this.StorageTypesLimited;
      foreach(var clrType in storageType.MapToTypes) {
        Register(typeSet, clrType, storageType);
      }
      StorageTypesAll.Add(storageType);
      return storageType;
    }

    private static void Register(DbStorageTypeMap typeSet, Type clrType, DbStorageType stype) {
      var exists = typeSet.ContainsKey(clrType);
      Util.Check(!exists, "Failed to register storage type {0} for CLR type {1}, CLR type is already mapped.", stype.TypeName, clrType);
      typeSet.Add(clrType, stype); 
    }

    public virtual DbStorageType FindStorageType(Type clrType, bool unlimited) {
      var type = clrType.GetUnderlyingStorageType();
      DbStorageType stype;
      if(unlimited)
        StorageTypesUnlimited.TryGetValue(type, out stype);
      else
        StorageTypesLimited.TryGetValue(type, out stype);
      return stype;
    }

    public virtual DbStorageType FindDbTypeDef(string typeName, bool unlimited) {
      typeName = typeName.ToLowerInvariant();
      var storageType = StorageTypesAll.FirstOrDefault(td => (td.LoadTypeName == typeName || td.Aliases.Contains(typeName)) 
                                                     && td.IsUnlimited == unlimited);
      return storageType;
    }

    public virtual DbStorageType FindDbTypeDef(DbType dbType, Type clrType, bool unlimited) {
      var matches = StorageTypesAll.Where(vti => vti.DbType == dbType && vti.IsUnlimited == unlimited).ToList(); // && vti.MapToTypes.Contains(clrType));
      switch(matches.Count) {
        case 0: return null;
        case 1: return matches[0];
      }
      // we have more than 1 match
      var match = matches.FirstOrDefault(s => s.ColumnOutType == clrType || s.MapToTypes.Contains(clrType));
      if(match != null)
        return match;
      // just return first match
      return matches[0]; 

    }


    public virtual DbColumnTypeInfo GetColumnTypeInfo(EntityMemberInfo forMember, IActivationLog log) {
      bool isUnlimited = forMember.Flags.IsSet(EntityMemberFlags.UnlimitedSize);
      //Storage data type is derived from member data type. For Nullable<T>, it is underlying type T; for enums, it is underlying int type
      var columnType = forMember.DataType.GetUnderlyingStorageType();
      //Find DbTypeDefinition
      DbStorageType typeDef = null;
      //Check if DbTypeSpec is provided explicitly through attribute
      string typeSpec = null; 
      if (!string.IsNullOrWhiteSpace(forMember.ExplicitDbTypeSpec)) {
        typeSpec = forMember.ExplicitDbTypeSpec;
        string typeName;
        string strTypeArgs = null;
        UnpackTypeSpec(forMember.ExplicitDbTypeSpec, out typeName, out strTypeArgs);
        typeDef = FindDbTypeDef(typeName, isUnlimited);
      } else 
        //Otherwise check if DbType is provided explicitly
        if (forMember.ExplicitDbType != null)
          typeDef = FindDbTypeDef(forMember.ExplicitDbType.Value, columnType, isUnlimited);
      else
        // Otherwise find default for Clr type.
        typeDef = FindStorageType(columnType, isUnlimited);
      if (typeDef == null)
        return null;
      //create db typeinfo
      //figure out type arguments and format them
      if (typeSpec == null) {
        var typeArgs = DbColumnTypeInfo.GetTypeArgs(forMember.Size, forMember.Precision, forMember.Scale);
        typeSpec = typeDef.FormatTypeSpec(typeArgs);
      }
      var isNullable = forMember.Flags.IsSet(EntityMemberFlags.Nullable);
      var colDefault = forMember.ColumnDefault ?? typeDef.DefaultColumnInit;
      var typeInfo = new DbColumnTypeInfo(typeDef, typeSpec, isNullable, forMember.Size, forMember.Precision, forMember.Scale,
                   colDefault);
      // Assign value converters
      if(typeDef.ColumnOutType != forMember.DataType) {
        var conv = Converters.GetConverter(typeInfo.StorageType.ColumnOutType, forMember.DataType);
        if(conv == null) {
          log.Error("Converter from type {0} to type {1} not found.", typeInfo.StorageType.ColumnOutType, forMember.DataType);
        } else {
          typeInfo.ColumnToPropertyConverter = conv.ColumnToProperty;
          typeInfo.PropertyToColumnConverter = conv.PropertyToColumn;
        }
      }
      return typeInfo; 
    }


    static Func<object, object> _noConv = x => x; 

    public virtual Func<object, object> GetLinqValueConverter(Type linqExpressionType, Type finalType) {
      var adjustedExprType = linqExpressionType.GetUnderlyingStorageType(); //account for nullable and enums
      var typeDef = FindStorageType(adjustedExprType, false);
      Util.Check(typeDef != null, "Failed to determine type returned by database for LINQ expression, type: {0}", linqExpressionType);
      var colOutType = typeDef.ColumnOutType;
      if(colOutType == finalType)
        return _noConv;
      // DB might not support the data type directly; ex: SQLite stores decimals as doubles
      // so 'Max(decimalProp)' - Linq expression returns decimal but SQL will return double. We need to convert the storage type to LINQ expr type
      var convObj = Converters.GetConverter(colOutType, finalType);
      Util.Check(convObj != null, "Failed to find converter from DB type {0} to type {1}", colOutType, linqExpressionType);
      return convObj.ColumnToProperty;
    }

    public virtual string GetDefaultColumnInitExpression(Type type) {
      if (type == typeof(string)) return "''";
      if (type.IsInt()) return "0";
      if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "0";
      if (type == typeof(Guid)) return "'" + Guid.Empty + "'";
      if (type == typeof(DateTime)) return "'1900-01-01'";
      if (type == typeof(bool)) return "0";
      return null;
    }

    static void UnpackTypeSpec(string typeSpec, out string typeName, out string args) {
      var parIndex = typeSpec.IndexOf('(');
      if (parIndex > 0) {
        typeName = typeSpec.Substring(0, parIndex);
        args = typeSpec.Substring(parIndex);
      } else {
        typeName = typeSpec;
        args = null;
      }
    }

    public virtual string GetListLiteral(object value) {
      var valueType = value.GetType();
      Util.Check(valueType.IsListOfDbPrimitive(out Type elemType),
          "Value must be list of DB primitives. Value type: {0} ", valueType);
      var elemTypeDef = FindStorageType(elemType, false);
      Util.Check(elemTypeDef != null, "Failed to find DbTypeInfo for array element type {0}.", elemType);
      var list = value as IList;
      if(list.Count == 0)
        return GetEmptyListLiteral(elemTypeDef);
      var strList = new List<string>(list.Count);
      foreach(var item in list) {
        strList.Add(elemTypeDef.ValueToLiteral(item));
      }
      return string.Join(", ", strList);
    }

    // Used in 't.Col IN (<list>)' expressions when list is empty; does not work for all providers
    public virtual string GetEmptyListLiteral(DbStorageType elemTypeDef) {
      return "SELECT NULL WHERE 1=0"; //works OK for MS SQL, SQLite
    }


  }//class

}//ns

