using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities;
using Vita.Entities.Utilities;
using Vita.Data.Driver.TypeSystem;

namespace Vita.Data.Driver {

  public static class DbDriverUtil {

    public static bool IsSet(this DbTypeFlags flags, DbTypeFlags flag) {
      return (flags & flag) != 0;
    }

    public static Type GetUnderlyingStorageClrType(this Type type) {
      if(type.IsNullableValueType())
        type = Nullable.GetUnderlyingType(type);
      if(type.IsEnum)
        return Enum.GetUnderlyingType(type);
      if(type == typeof(Binary))
        return typeof(byte[]);
      return type;
    }

    public static byte[] GetBytes(object value) {
      switch(value) {
        case byte[] bytes:
          return bytes;
        case Guid g:
          return g.ToByteArray();
        case Binary bin:
          return bin.GetBytes();
        default:
          return null;
      }
    }


  }
}
