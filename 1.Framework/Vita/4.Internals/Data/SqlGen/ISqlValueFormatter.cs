using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.SqlGen;
using Vita.Entities.Runtime;

namespace Vita.Data.SqlGen {

  [Flags]
  public enum SqlFormatOptions {
    Auto = 0, 
    PreferParam = 1,
    PreferLiteral = 1 << 1,
    NoParameters = 1 << 2,
  }

  public interface ISqlValueFormatter {
    SqlFragment FormatValue(DbStorageType typeDef, object value, SqlFormatOptions options);
    SqlFragment FormatValue(EntityRecord record, DbColumnInfo column, SqlFormatOptions options);
  }

  public static partial class SqlGenExtensions {
    public static bool IsSet(this SqlFormatOptions options, SqlFormatOptions option) {
      return (options & option) != 0; 
    }
  }//class

}
