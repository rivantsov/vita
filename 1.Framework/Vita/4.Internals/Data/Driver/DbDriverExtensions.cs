using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Data.Driver {

  public static class DbDriverExtensions {

    public static bool IsSet(this DbTypeFlags flags, DbTypeFlags flag) {
      return (flags & flag) != 0;
    }

    public static Type GetUnderlyingStorageType(this Type type) {
      if(type.IsNullableValueType())
        type = Nullable.GetUnderlyingType(type);
      if(type.IsEnum)
        return Enum.GetUnderlyingType(type);
      if(type == typeof(Binary))
        return typeof(byte[]);
      return type;
    }


  }
}
