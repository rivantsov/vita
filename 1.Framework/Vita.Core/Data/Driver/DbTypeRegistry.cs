using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Entities.Logging;

namespace Vita.Data.Driver {

  public class DbTypeRegistry {
    protected DbDriver Driver; 

    public IList<VendorDbTypeInfo> Types = new List<VendorDbTypeInfo>();
    public HashSet<string> AutoMemoTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public DbValueConverterRegistry Converters = new DbValueConverterRegistry();

    public DbTypeRegistry(DbDriver driver) {
      Driver = driver; 
    }

    public VendorDbTypeInfo AddType(string typeName, DbType dbType, Type columnOutType,
                                    bool isDefault = true, bool supportsMemo = false, bool isSubType = false,
                                    string args = null, string memoArgs = null,
                                    Type[] clrTypes = null, Type dbFirstClrType = null, string aliases = null, string columnInit = null,
                                    int vendorDbType = -1, DbValueToLiteralConvertFunc valueToLiteral = null) {
      //validate that we have only one default type for clr type
      if (isDefault && clrTypes != null) {
        foreach(var clrType in clrTypes) {
          var oldType = Types.FirstOrDefault(td => td.ClrTypes.Contains(clrType) && td.Flags.IsSet(VendorDbTypeFlags.IsDefaultForClrType));
          Util.Check(oldType == null, "There is already Db type definition registered as default for CLR type {0}.", columnOutType);
        }
      }      
      typeName = typeName.ToLowerInvariant();
      var flags = VendorDbTypeFlags.None;
      if (isDefault) flags |= VendorDbTypeFlags.IsDefaultForClrType;
      if (supportsMemo) flags |= VendorDbTypeFlags.SupportsUnlimitedSize;
      if (isSubType) flags |= VendorDbTypeFlags.IsSubType;

      columnInit = columnInit ?? GetDefaultColumnInitExpression(columnOutType);
      var typeDef = new VendorDbTypeInfo(typeName, dbType, columnOutType, args, memoArgs, clrTypes, dbFirstClrType, 
                                         flags, aliases, valueToLiteral, vendorDbType, columnInit);
      Types.Add(typeDef);
      return typeDef;
    }

    public VendorDbTypeInfo AddType<TVendorType>(string typeName, DbType dbType, Type columnOutType, TVendorType vendorDbType,
                                    bool isDefault = true, bool supportsMemo = false, bool isSubType = false,
                                    string args = null, string memoArgs = null,
                                    Type[] clrTypes = null, Type dbFirstClrType = null, string aliases = null, string columnInit = null,
                                    DbValueToLiteralConvertFunc valueToLiteral = null) where TVendorType : struct 
    {
      return AddType(typeName, dbType, columnOutType, isDefault, supportsMemo, isSubType, args, memoArgs, clrTypes, dbFirstClrType, aliases, columnInit, 
                     (int)(object)vendorDbType, valueToLiteral);
    }

    public void RegisterAutoMemoTypes(params string[] typeNames) {
      AutoMemoTypes.UnionWith(typeNames); 
    }

    public virtual DbTypeInfo GetDbTypeInfo(EntityMemberInfo member, MemoryLog log) {
      bool isUnlimited = member.Flags.IsSet(EntityMemberFlags.UnlimitedSize);
      //Storage data type is derived from member data type. For Nullable<T>, it is underlying type T; for enums, it is underlying int type
      var columnType = GetUnderlyingClrType(member.DataType);
      //Find DbTypeDefinition
      VendorDbTypeInfo vendorType = null;
      //Check if DbTypeSpec is provided explicitly through attribute
      string typeSpec = null; 
      if (!string.IsNullOrWhiteSpace(member.ExplicitDbTypeSpec)) {
        typeSpec = member.ExplicitDbTypeSpec;
        string typeName;
        string typeArgs = null;
        UnpackTypeSpec(member.ExplicitDbTypeSpec, out typeName, out typeArgs);
        if(AutoMemoTypes.Contains(typeName) && member.Size == 0) {
          //if size was not specified explicitly, mark member/column as memo
          isUnlimited = true;
          member.Flags |= EntityMemberFlags.UnlimitedSize;
          member.Size = -1;
        }
        vendorType = FindVendorDbTypeInfo(typeName);
      } else 
        //Otherwise check if DbType is provided explicitly
        if (member.ExplicitDbType != null)
          vendorType = FindVendorDbTypeInfo(member.ExplicitDbType.Value, columnType, isUnlimited);
      else
        // Otherwise find default for Clr type.
        vendorType = FindVendorDbTypeInfo(columnType, isUnlimited);
      if (vendorType == null)
        return null; 
      //create db typeinfo
      //figure out type arguments and format them
      typeSpec = typeSpec ?? vendorType.FormatTypeSpec(member.Size, member.Precision, member.Scale, isUnlimited);
      var isNullable = member.Flags.IsSet(EntityMemberFlags.Nullable);
      var colDefault = member.ColumnDefault ?? vendorType.DefaultColumnInit;
      var typeInfo = new DbTypeInfo(vendorType, typeSpec, isNullable, member.Size, member.Precision, member.Scale, colDefault);
      // Assign value converters
      if(vendorType.ColumnOutType != member.DataType) {
        var conv = Converters.GetConverter(typeInfo.VendorDbType.ColumnOutType, member.DataType);
        if(conv == null) {
          log.Error("Converter from type {0} to type {1} not found.", typeInfo.VendorDbType.ColumnOutType, member.DataType);
        } else {
          typeInfo.ColumnToPropertyConverter = conv.ColumnToProperty;
          typeInfo.PropertyToColumnConverter = conv.PropertyToColumn;
        }
      }
      return typeInfo; 
    }

    //used for creating db type info for parameters, when there's no member
    public virtual DbTypeInfo GetDbTypeInfo(Type dataType, int size) {
      Type elemType;
      if (dataType.IsListOfDbPrimitive(out elemType))
        return ConstructArrayTypeInfo(elemType);
      bool isMemo = size < 0;
      var colType = dataType;
      var typeDef = FindVendorDbTypeInfo(colType, isMemo);
      Util.Check(typeDef != null, "Failed to find vendor DB type for CLR type {0}.", dataType);
      var typeSpec = typeDef.FormatTypeSpec(size, 18, 4, isMemo);
      var typeInfo = new DbTypeInfo(typeDef, typeSpec, true, size, 0, 0, typeDef.DefaultColumnInit);
      var colOutType = typeInfo.VendorDbType.ColumnOutType;
      if (colOutType != dataType) {
        var conv = Converters.GetConverter(colOutType, dataType);
        Util.Check(conv != null, "Failed to find value converter for types {0} <-> {1}", colOutType, dataType);
        typeInfo.ColumnToPropertyConverter = conv.ColumnToProperty;
        typeInfo.PropertyToColumnConverter = conv.PropertyToColumn;
      }
      return typeInfo;
    }


    static Func<object, object> _noConv = x => x; 

    public virtual Func<object, object> GetLinqValueConverter(Type linqExpressionType, Type finalType) {
      var adjustedExprType = GetUnderlyingClrType(linqExpressionType); //account for nullable and enums
      var typeDef = FindVendorDbTypeInfo(adjustedExprType, false);
      Util.Check(typeDef != null, "Failed to determine type returned by database for LINQ expression, type: {0}", linqExpressionType);
      var storageType = typeDef.ColumnOutType;
      if(storageType == finalType)
        return _noConv;
      // There are 2 special cases when conversion might be necessary
      // First, the DB might not support the data type directly; ex: SQLite stores decimals as doubles
      // so 'Max(decimalProp)' - Linq expression returns decimal but SQL will return double. We need to convert the storage type to LINQ expr type
      var convObj = Converters.GetConverter(storageType, finalType);
      Util.Check(convObj != null, "Failed to find converter from DB type {0} to type {1}", storageType, linqExpressionType);
      return convObj.ColumnToProperty;
    }

    public virtual DbTypeInfo ConstructArrayTypeInfo(Type elemType) {
      return null; 
    }

    protected Type GetUnderlyingClrType(Type type) {
      if (type == typeof(Binary))
        return typeof(byte[]);
      if (type.IsNullableValueType())
        type = Nullable.GetUnderlyingType(type);
      if (type.IsEnum)
        return Enum.GetUnderlyingType(type);
      return type;
    }

    public virtual VendorDbTypeInfo FindVendorDbTypeInfo(string typeName) {
      VendorDbTypeInfo vendorType;
      var name = typeName.ToLowerInvariant();
      // Note: exclude subTypes - they are only for looking up by specific CLR types
      vendorType = Types.FirstOrDefault(td => td.TypeName == name && !td.Flags.IsSet(VendorDbTypeFlags.IsSubType));
      //if not found, look by alias
      if (vendorType == null)
        vendorType = Types.FirstOrDefault(td => td.Aliases.Contains(name) && !td.Flags.IsSet(VendorDbTypeFlags.IsSubType));
      return vendorType; 
    }

    public virtual VendorDbTypeInfo FindVendorDbTypeInfo(DbType dbType, Type clrType, bool isMemo) {
      var nonMemoOk = !isMemo;
      var match = Types.FirstOrDefault(vti => vti.DbType == dbType && 
           (vti.HandlesMemo || nonMemoOk) && vti.ClrTypes.Contains(clrType));
      return match;
    }    

    public virtual VendorDbTypeInfo FindVendorDbTypeInfo(Type clrType, bool isMemo) {
      if (clrType.IsNullableValueType())
        clrType = Nullable.GetUnderlyingType(clrType); 
      bool nonMemoOk = !isMemo;
      //First try to find typedef that with matching ColumnOutType
      var typeDef = Types.FirstOrDefault(td => td.ColumnOutType == clrType && td.Flags.IsSet(VendorDbTypeFlags.IsDefaultForClrType) && (nonMemoOk || td.HandlesMemo));
      if (typeDef != null)
        return typeDef;
      //Find any type that has clrType in ClrTypes list (compatible types)
      typeDef = Types.FirstOrDefault(td => td.ClrTypes.Contains(clrType) && (nonMemoOk || td.HandlesMemo));
      if (typeDef != null)
        return typeDef;
      return null; 
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

  }//class

  [Flags]
  public enum VendorDbTypeFlags {
    None = 0,
    IsDefaultForClrType = 1,
    SupportsUnlimitedSize = 1 << 1, //Can handle Memo fields (text or binary)
    IsSubType = 1 << 2, //is specialization of a db type, for specific CLR type. Ex: binary(16) for Guid
    UserDefined = 1 << 3, 
    Array = 1 << 4,
  }

  public class VendorDbTypeInfo {
    public string TypeName;
    public DbType DbType;
    public string ArgsTemplate; //args in parenthesis, ex: "({length})", "({precision},{scale})", "(max)"
    public string ArgsTemplateUnlimitedSize; // args for the case when db type is memo (unlimited size); only if the type supports Memo
    // Primary CLR type associated with db type. Must be the type of value in the column in data reader
    public Type ColumnOutType;
    // Type assigned to member in db-first scenario; by default is the same as ColumnOutType
    public Type DbFirstClrType;
    // CLR types compatible with this db type; ColumnOutType, DbFirstClrType are automatically added
    public HashSet<Type> ClrTypes = new HashSet<Type>();

    public VendorDbTypeFlags Flags;
    public List<string> Aliases = new List<string>();
    public string DefaultColumnInit;
    // Vendor-specific db type; might be used for SqlParameter.SqlDbType (MS SQL Server); currently not used
    public int VendorDbType;
    public DbValueToLiteralConvertFunc ValueToLiteral;

    public VendorDbTypeInfo(string typeName, DbType dbType, Type columnOutType,
                            string argsTemplate, string argsTemplateUnlimitedSize,
                            Type[] clrTypes, Type dbFirstClrType, VendorDbTypeFlags flags, string aliases,
                            DbValueToLiteralConvertFunc valueToLiteral,
                            int vendorDbType = -1, string columnInit = null) {
      TypeName = typeName;
      DbType = dbType;
      ArgsTemplate = argsTemplate;
      ArgsTemplateUnlimitedSize = argsTemplateUnlimitedSize ?? argsTemplate;
      ColumnOutType = columnOutType;
      DbFirstClrType = dbFirstClrType ?? ColumnOutType;
      if(clrTypes != null)
        ClrTypes.UnionWith(clrTypes);
      ClrTypes.Add(ColumnOutType);
      ClrTypes.Add(DbFirstClrType); 
      Flags = flags;
      if(!string.IsNullOrWhiteSpace(aliases))
        Aliases.AddRange(aliases.SplitNames(',', ';'));
      VendorDbType = vendorDbType;
      DefaultColumnInit = columnInit;
      ValueToLiteral = valueToLiteral ?? DbValueToLiteralConverters.GetDefaultToLiteralConverter(columnOutType);
    }

    public override string ToString() {
      return TypeName + ArgsTemplate;
    }
    public bool HandlesMemo {
      get { return Flags.IsSet(VendorDbTypeFlags.SupportsUnlimitedSize); }
    }

    public virtual string FormatTypeSpec(long size, int precision, int scale, bool isUnlimited = false) {
      var argsTemplate = isUnlimited ? this.ArgsTemplateUnlimitedSize : this.ArgsTemplate;
      if(string.IsNullOrEmpty(argsTemplate) || !argsTemplate.Contains("{"))
        return TypeName + argsTemplate;
      var result = TypeName +
        argsTemplate.ToLowerInvariant()
        .Replace("{size}", size.ToString())
        .Replace("{precision}", precision.ToString())
        .Replace("{scale}", scale.ToString());
      return result;
    }

  } //class

}//ns

