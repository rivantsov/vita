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

    // IDbTypeRegistry implementation
    public virtual DbTypeInfo GetDbTypeInfo(string typeName, long size = 0, byte prec = 0, byte scale = 0) {
      if(!TypeDefsByName.TryGetValue(typeName, out DbTypeDef typeDef))
        return null;
      if(typeDef.DefaultTypeInfo != null) 
        return typeDef.DefaultTypeInfo;
      if(size == 0 && typeDef.Flags.IsSet(DbTypeFlags.Unlimited))
        size = -1;
      // we have storage type that requires arguments (ex: varchar). Create mapping with args
      var typeSpec = FormatTypeSpec(typeDef, size, prec, scale); 
      return CreateDbTypeInfo(typeDef.ColumnOutType, typeDef, size, prec, scale);
    }

    public virtual DbTypeInfo GetDbTypeInfo(EntityMemberInfo forMember) {
      var colType = forMember.DataType.GetUnderlyingStorageClrType();
      if(DbTypesByClrType.TryGetValue(colType, out var mapping))
          return mapping;
      // direct mapping by col type (CLR type) not found. 
      DbTypeDef storageType = null;
      // find by value kind
      var mappedTo = GetDbSpecialType(colType, forMember);
      if (mappedTo != DbSpecialType.None)
        SpecialTypeDefs.TryGetValue(mappedTo, out storageType);
      // if not found try by col type
      if (storageType == null) {
        TypeDefsByColumnOutType.TryGetValue(colType, out storageType);
      }
      if (storageType == null)
        return null; 
      // found storage type; try default mapping
      if (storageType.DefaultTypeInfo != null)
        return storageType.DefaultTypeInfo;
      // create new mapping
      return CreateDbTypeInfo(colType, storageType, forMember.Size, forMember.Precision, forMember.Scale);
    }

    public virtual DbTypeInfo GetDbTypeInfo(string typeSpec, EntityMemberInfo forMember) {
      var dataType = forMember.DataType;
      var fullName = forMember.Entity.Name + "." + forMember.MemberName;
      var parsedTypeSpec = this.ParseTypeSpec(typeSpec);
      if(!TypeDefsByName.TryGetValue(parsedTypeSpec.TypeName, out var storageType))
        Util.Throw($"Member {fullName}: failed to map DB type {parsedTypeSpec.TypeName}.");
      if(storageType.DefaultTypeInfo != null)
        return storageType.DefaultTypeInfo; 
      var baseType = forMember.DataType.GetUnderlyingStorageClrType();
      var mapping = MapWithArgs(baseType, storageType, parsedTypeSpec.Args);
      return mapping;
    }

    // this method is used at runtime in LINQ queries
    public virtual DbTypeDef GetDbTypeDef(Type dataType) {
      var colType = dataType.GetUnderlyingStorageClrType();
      if (DbTypesByClrType.TryGetValue(colType, out DbTypeInfo typeInfo))
        return typeInfo.TypeDef;
      // for string make quick lookup
      if (colType == typeof(string) && SpecialTypeDefs.TryGetValue(DbSpecialType.String, out var typeDef)) {
        return typeDef; 
      }
      // make plain search for matching type
      return TypeDefsByName.Values.FirstOrDefault(td => td.ColumnOutType == colType && !td.Flags.IsSet(DbTypeFlags.Unlimited));
    }

    public DbValueConverter GetDbValueConverter(Type columnType, Type memberType) {
      return this.Converters.GetConverter(columnType, memberType);
    }

    public virtual DbValueConverter GetDbValueConverter(DbTypeInfo mapping, Type memberType) {
      return this.GetDbValueConverter(mapping.ColumnOutType, memberType); 
    }
  } //class


} //ns
