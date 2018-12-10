using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;

using Vita.Entities.Logging;
using Vita.Entities.Model;

namespace Vita.Data.Driver.TypeSystem {

  using ToLiteralFunc = Func<object, string>;

  public interface IDbTypeRegistry {
    // used by DbModelLoader
    DbTypeInfo GetDbTypeInfo(string typeName, long size = 0, byte prec = 0, byte scale = 0);
    // DBModel builder, mapping from entity members to db types
    DbTypeInfo GetDbTypeInfo(EntityMemberInfo forMember);
    DbTypeInfo GetDbTypeInfo(string typeSpec, EntityMemberInfo forMember);
    // Used by linq and sequence definition
    DbTypeDef GetDbTypeDef(Type dataType);
    // Used by Linq - by default goes to Converters dict
    DbValueConverter GetDbValueConverter(Type columnType, Type memberType);

    DbValueConverter GetDbValueConverter(DbTypeInfo typeInfo, Type memberType);
  }



  [Flags]
  public enum DbTypeFlags {
    None = 0,
    Size = 1,
    Precision = 1 << 1,
    Scale = 1 << 2,
    //ArgsOptional = 1 << 3,
    Unlimited = 1 << 4,
    IsCustomType = 1 << 5,

    Obsolete = 1 << 9,
    Ansi = 1 << 10,

    PrecisionScale = Precision | Scale,
    HasArgs = Size | Precision | Scale,
  }

  public enum DbSpecialType {
    None,
    String,
    StringUnlimited,
    StringAnsi,
    StringAnsiUnlimited,
    Binary,
    BinaryUnlimited,
  }

  public class DbTypeDef {
    public string Name;
    public DbTypeFlags Flags;
    public Type ColumnOutType;
    public byte? DefaultPrecision;
    public byte? DefaultScale; 
    public DbTypeInfo DefaultTypeInfo;
    public IList<string> Aliases = new List<string>();
    public string ColumnInit;
    public ToLiteralFunc ToLiteral;
    // Used only by Postgres, in array parameters - it requires explicit NpgDbType of elem in combination with Array flag
    public int ProviderDbType;

    public override string ToString() {
      return $"{Name}";
    }


  }

  public class DbTypeInfo {
    public Type ClrType;
    public DbTypeDef TypeDef;
    public string DbTypeSpec;
    public Type ColumnOutType; 
    public Func<IDataRecord, int, object> ColumnReader; 

    public long Size;
    public byte Precision;
    public byte Scale;

    public DbTypeInfo(DbTypeDef typeDef, Type clrType, string typeSpec, long size, byte prec, byte scale) {
      ClrType = clrType;
      TypeDef = typeDef;
      Size = size;
      Precision = prec;
      Scale = scale;
      DbTypeSpec = typeSpec;
      if(typeDef.Flags.IsSet(DbTypeFlags.Unlimited))
        Size = -1;
      ColumnOutType = typeDef.ColumnOutType; 
      ColumnReader = ReadColumnValue;
    }

    public override string ToString() {
      return $"{ClrType}->{DbTypeSpec}(DB)";
    }
    public bool Matches(DbTypeInfo other) {
      if (other == null)
        return false;
      if (other == this)
        return true;
      return other.TypeDef == this.TypeDef && other.ClrType == this.ClrType &&
          other.Size == this.Size && other.Precision == this.Precision && other.Scale == this.Scale;
    }
    public bool Matches(long size, byte precision, byte scale) {
      return size == this.Size && precision == this.Precision && scale == this.Scale; 
    }
    public object ReadColumnValue (IDataRecord rec, int columnIndex) {
      return rec.GetValue(columnIndex);
    }
  }

}
