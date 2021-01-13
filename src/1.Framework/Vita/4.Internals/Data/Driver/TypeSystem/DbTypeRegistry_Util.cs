using System;
using System.Collections.Generic;
using System.Linq;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Data.Driver.TypeSystem {
  using ToLiteralFunc = Func<object, string>;

  public partial class DbTypeRegistry {

    public class ParsedDbTypeSpec {
      public string TypeName;
      public int[] Args;
    }

    static int[] _emptyInts = new int[] { };
    protected virtual ParsedDbTypeSpec ParseTypeSpec(string typeSpec) {
      string typeName;
      try {
        var parIndex = typeSpec.IndexOf('(');
        if(parIndex <= 0)
          return new ParsedDbTypeSpec() { TypeName = typeSpec, Args = _emptyInts };
        typeName = typeSpec.Substring(0, parIndex);
        var strArgs = typeSpec.Substring(parIndex + 1, typeSpec.Length - parIndex - 2);
        Util.Check(!string.IsNullOrWhiteSpace(strArgs), "Missing type arguments");
        var args = strArgs.Split(',').Select(s => int.Parse(s)).ToArray();
        Util.Check(args.Length <= 3, "Invalid number of arguments");
        return new ParsedDbTypeSpec() { TypeName = typeName, Args = args };
      } catch(Exception ex) {
        throw new Exception($"Invalid type spec: '{typeSpec}', error: {ex.Message}");
      }
    }

    public virtual string FormatTypeSpec(DbTypeDef typeDef, long size = 0, byte prec = 0, byte scale = 0) {
      var name = typeDef.Name;
      var flags = typeDef.Flags;
      if(!flags.IsSet(DbTypeFlags.HasArgs))
        return name;
      //Size
      if(flags.IsSet(DbTypeFlags.Size)) {
        if(size < 0)
          return name;
        else
          return $"{name}({size})";
      }
      // precision, scale - both
      if(flags.IsSet(DbTypeFlags.Precision) && flags.IsSet(DbTypeFlags.Scale)) {
        if(prec != 0 && scale != 0)
          return $"{name}({prec}, {scale})";
        if(prec != 0)
          return $"{name}({prec})";
        if(scale != 0)
          return $"{name}({scale})";
      }
      // prec only
      if(flags.IsSet(DbTypeFlags.Precision) && prec != 0)
        return $"{name}({prec})";
      // scale only
      if(flags.IsSet(DbTypeFlags.Scale) && scale != 0)
        return $"{name}({scale})";
      // default 
      return name;
    } //method


    public virtual DbTypeInfo MapWithArgs(Type clrType, DbTypeDef typeDef, int[] args) {
      var flags = typeDef.Flags;
      if (flags.IsSet(DbTypeFlags.Size)) {
        Util.Check(args.Length == 1, "Invalid DbTypeSpec ({0}, expected size parameter.");
        return CreateDbTypeInfo(clrType, typeDef, size: args[0]);
      }
      if(flags.IsSet(DbTypeFlags.Precision | DbTypeFlags.Scale)) {
        switch(args.Length) {
          case 1:
            return CreateDbTypeInfo(clrType, typeDef, prec: (byte)args[0], scale: 0);
          case 2:
            return CreateDbTypeInfo(clrType, typeDef, prec: (byte) args[0], scale: (byte) args[1]);
        }
      }
      return CreateDbTypeInfo(clrType, typeDef);
    } //method

    protected virtual DbTypeInfo CreateDbTypeInfo(Type clrType, DbTypeDef typeDef, long size = 0, byte prec = 0, byte scale = 0) {
      if(prec == 0 && typeDef.DefaultPrecision != null)
        prec = typeDef.DefaultPrecision.Value;
      if(scale == 0 && typeDef.DefaultScale != null)
        scale = typeDef.DefaultScale.Value; 
      var typeSpec = FormatTypeSpec(typeDef, size, prec, scale);
      return new DbTypeInfo(typeDef, clrType, typeSpec, size, prec, scale);
    }


    protected virtual DbSpecialType GetDbSpecialType(Type colType, EntityMemberInfo forMember) {
      if(colType == typeof(string)) {
        var colAttr = forMember.GetAttribute<ColumnAttribute>();
        var ansi = colAttr != null && colAttr.AnsiString;
        var unlimited = forMember.Size < 0;
        if(ansi)
          return unlimited ? DbSpecialType.StringAnsiUnlimited : DbSpecialType.StringAnsi;
        else
          return unlimited ? DbSpecialType.StringUnlimited : DbSpecialType.String;

      } else if(colType == typeof(byte[])) {
        return forMember.Size < 0 ? DbSpecialType.BinaryUnlimited : DbSpecialType.Binary;

      } else
        return DbSpecialType.None; 
    }

    public virtual string GetDefaultColumnInitExpression(Type type) {
      if (type == typeof(string))
        return "''";
      if (type.IsInt())
        return "0";
      if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        return "0";
      if (type == typeof(Guid))
        return "'" + Guid.Empty + "'";
      if (type == typeof(DateTime))
        return "'1900-01-01'";
      if (type == typeof(bool))
        return "0";
      return null;
    }



  } //class


}
