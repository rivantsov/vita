using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Data.Driver.TypeSystem {
  using ToLiteralFunc = Func<object, string>;

  public partial class DbTypeRegistry : IDbTypeRegistry {
    public readonly DbDriver Driver;

    public Dictionary<string, DbTypeDef> TypeDefsByName = new Dictionary<string, DbTypeDef>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<Type, DbTypeDef> TypeDefsByColumnOutType = new Dictionary<Type, DbTypeDef>();
    public Dictionary<DbSpecialType, DbTypeDef> SpecialTypeDefs = new Dictionary<DbSpecialType, DbTypeDef>();

    // direct mappings for types that do not need args (size, prec) and are not unlimited
    public ConcurrentDictionary<Type, DbTypeInfo> DbTypesByClrType = new ConcurrentDictionary<Type, DbTypeInfo>();

    public DbValueConverterRegistry Converters = new DbValueConverterRegistry();

    public DbTypeRegistry(DbDriver driver) {
      Driver = driver;
    }

    // Newer version (10-29)
    private string[] _emptyStrings = new string[] { };

    public virtual DbTypeDef AddDbTypeDef(string name, Type columnOutType, DbTypeFlags flags = DbTypeFlags.None,
                  string specTemplate = null, string aliases = null,
                  byte? defaultPrecision = null, byte? defaultScale = null,
                  ToLiteralFunc toLiteral = null, string columnInit = null,
                  bool mapColumnType = true, //map to column out type if typeDef has no args and not unlimited
                  object providerDbType = null, //used by Postgres only
                  DbSpecialType specialType = DbSpecialType.None) {
      var arrAliases = string.IsNullOrEmpty(aliases) ? _emptyStrings : aliases.Split(',');
      toLiteral = toLiteral ?? DbValueToLiteralConverters.GetDefaultToLiteralConverter(columnOutType);
      columnInit = columnInit ?? GetDefaultColumnInitExpression(columnOutType);
      var typeDef = new DbTypeDef() { Name = name, ColumnOutType = columnOutType, Flags = flags, Aliases = arrAliases,
                     ToLiteral = toLiteral, ColumnInit = columnInit, DefaultPrecision = defaultPrecision, DefaultScale = defaultScale };
      TypeDefsByName.Add(name, typeDef);
      // register under aliases
      foreach(var alias in typeDef.Aliases)
        TypeDefsByName.Add(alias, typeDef);
      if(specialType != DbSpecialType.None) {
        Util.Check(!SpecialTypeDefs.ContainsKey(specialType), "TypeDef for special type {0} already registered.", specialType);
        SpecialTypeDefs[specialType] = typeDef;
      }
      // Register by columnOutType
      if (!TypeDefsByColumnOutType.ContainsKey(columnOutType))
        TypeDefsByColumnOutType[columnOutType] = typeDef;
      // If has a form without args, register it as default type def
      if (!flags.IsSet(DbTypeFlags.HasArgs)) {
        typeDef.DefaultTypeInfo = CreateDbTypeInfo(columnOutType, typeDef);
        if (mapColumnType && !flags.IsSet(DbTypeFlags.Unlimited) && !DbTypesByClrType.ContainsKey(columnOutType))
          DbTypesByClrType[columnOutType] = typeDef.DefaultTypeInfo; 
      }
      if(providerDbType != null)
        typeDef.ProviderDbType = (int)providerDbType;
      return typeDef; 
    }

    public virtual DbTypeInfo Map(Type clrType, DbTypeDef typeDef, long size = 0, byte prec = 0, byte scale = 0) {
      var  mapping = CreateDbTypeInfo(clrType, typeDef, size, prec, scale);
      DbTypesByClrType[clrType] = mapping;
      return mapping; 
    }

    public virtual void MapMany(Type[] clrTypes, DbTypeDef typeDef, long size = 0,
                       byte prec = 0, byte scale = 0, bool overwriteAsDefault = false) {
      foreach (var type in clrTypes)
        Map(type, typeDef, size, prec, scale);
    }

  } //class


} //ns
