using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Model;

namespace Vita.Data.Model {
  using FuncValueToLiteral = Func<DbTypeInfo, object, string>;

  [Flags]
  public enum DbTypeFlags {
    None = 0,
    IsDefaultForClrType = 1,
    SupportsUnlimitedSize = 1 << 1, //Can handle Memo fields (text or binary)
    IsSubType = 1 << 2, //is specialization of a db type, for specific CLR type. Ex: binary(16) for Guid
    UserDefined = 1 << 3,
  }

  public class DbTypeDefinition {
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
    public FuncValueToLiteral ValueToLiteral;

    public DbTypeFlags Flags;
    public List<string> Aliases = new List<string>();
    public string DefaultColumnInit;
    // Vendor-specific db type; might be used for SqlParameter.SqlDbType (MS SQL Server); currently not used
    public int VendorDbType;

    public DbTypeDefinition(string typeName, DbType dbType, Type columnOutType,
                            string argsTemplate, string argsTemplateUnlimitedSize,
                            Type[] clrTypes, Type dbFirstClrType, DbTypeFlags flags, string aliases, FuncValueToLiteral valueToLiteral,
                            int vendorDbType = -1, string columnInit = null) {
      TypeName = typeName;
      DbType = dbType;
      ArgsTemplate = argsTemplate;
      ArgsTemplateUnlimitedSize = argsTemplateUnlimitedSize ?? argsTemplate;
      ColumnOutType = columnOutType;
      DbFirstClrType = dbFirstClrType ?? ColumnOutType;
      if (clrTypes != null)
        ClrTypes.UnionWith(clrTypes);
      ClrTypes.Add(ColumnOutType);
      ClrTypes.Add(DbFirstClrType);
      Flags = flags;
      if (!string.IsNullOrWhiteSpace(aliases))
        Aliases.AddRange(aliases.Split(',', ';'));
      VendorDbType = vendorDbType;
      DefaultColumnInit = columnInit;
      ValueToLiteral = valueToLiteral;
    }

    public override string ToString() {
      return TypeName + ArgsTemplate;
    }
    public bool HandlesMemo {
      get { return Flags.IsSet(DbTypeFlags.SupportsUnlimitedSize); }
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

  public static class DbTypesHelper {
    public static bool IsSet(this DbTypeFlags flags, DbTypeFlags flag) {
      return (flags & flag) != 0;
    }

  }//class


}
