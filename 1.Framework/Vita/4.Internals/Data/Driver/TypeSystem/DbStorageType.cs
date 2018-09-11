using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Data.Driver {

  using DbValueToLiteralFunc = Func<object, string>;

  public class DbStorageType {
    public DbTypeRegistry Registry; 
    public string TypeName;
    public DbType DbType;
    public string ArgsTemplate; //args in parenthesis, ex: "({length})", "({precision},{scale})", "(max)"
    // Primary CLR type associated with db type. Must be the type of value in the column in data reader
    public Type ColumnOutType;
    // Type assigned to member in db-first scenario; by default is the same as ColumnOutType
    public Type DbFirstClrType;
    // CLR types compatible with this db type; ColumnOutType, DbFirstClrType are automatically added
    public HashSet<Type> MapToTypes = new HashSet<Type>();
    public string LoadTypeName; //type name when loading from Information_schema.Columns

    public DbTypeFlags Flags;
    public List<string> Aliases = new List<string>();
    public string DefaultColumnInit;
    // Vendor-specific db type; might be used for SqlParameter.SqlDbType (MS SQL Server); currently not used
    public int CustomDbType;
    public bool IsList;

    public DbValueToLiteralFunc ValueToLiteral;
    public Func<object, object> ConvertToTargetType;

    public DbStorageType(DbTypeRegistry registry, string typeName, DbType dbType, Type columnOutType,
                            string argsTemplate, Type[] mapToTypes, Type dbFirstClrType, DbTypeFlags flags, string aliases,
                            DbValueToLiteralFunc valueToLiteral,
                            int customDbType, string columnInit, string loadTypeName) {
      Registry = registry; 
      TypeName = typeName;
      DbType = dbType;
      ArgsTemplate = argsTemplate;
      ColumnOutType = columnOutType;
      DbFirstClrType = dbFirstClrType ?? ColumnOutType;
      if(mapToTypes != null)
        MapToTypes.UnionWith(mapToTypes);
      Flags = flags;
      if(!string.IsNullOrWhiteSpace(aliases))
        Aliases.AddRange(aliases.SplitNames(',', ';'));
      CustomDbType = customDbType;
      DefaultColumnInit = columnInit;
      ValueToLiteral = valueToLiteral ?? DbValueToLiteralConverters.GetDefaultToLiteralConverter(columnOutType);
      ConvertToTargetType = DefaultConvertToTargetType;
      LoadTypeName = loadTypeName ?? TypeName; 
    }

    public override string ToString() {
      return TypeName + ArgsTemplate;
    }
    public bool IsUnlimited {
      get { return Flags.IsSet(DbTypeFlags.Unlimited); }
    }

    public virtual string FormatTypeSpec(object[] args) {
      if(string.IsNullOrEmpty(ArgsTemplate) || !ArgsTemplate.Contains("{") )
        return TypeName + ArgsTemplate;
      var result = TypeName +  string.Format(ArgsTemplate, args); 
      return result;
    }

    public object DefaultConvertToTargetType(object value) {
      if(value == null || value == DBNull.Value)
        return DBNull.Value;
      var type = value.GetType();
      if(type == this.ColumnOutType)
        return value;
      // need conv
      var converter = this.Registry.Converters.GetConverter(ColumnOutType, type);
      Util.Check(converter != null, "Failed to find converter {0}->{1}", type, ColumnOutType);
      return converter.PropertyToColumn(value); 
    }
  } //class


}
