using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

using Vita.Entities;
using Vita.Data.Model;
using Vita.Entities.Runtime;
using Vita.Data.Driver;

namespace Vita.Data {

  public static class DbExtensions {

    //Enum extensions
    public static bool IsSet(this DbOptions options, DbOptions option) {
      return (options & option) != 0;
    }
    public static bool IsSet(this DbFeatures features, DbFeatures feature) {
      return (features & feature) != 0;
    }
    public static bool IsSet(this DbUpgradeOptions options, DbUpgradeOptions option) {
      return (options & option) != 0;
    }

    public static bool IsSet(this VendorDbTypeFlags flags, VendorDbTypeFlags flag) {
      return (flags & flag) != 0;
    }

    public static bool IsSet(this ConnectionFlags flags, ConnectionFlags flag) {
      return (flags & flag) != 0;
    }



    public static string GetAsString(this DataRow row, string columnName) {
      var value = row[columnName];
      return value == DBNull.Value ? null : (string)value;
    }
    public static int GetAsInt(this DataRow row, string columnName) {
      var value = row[columnName];
      if (value == null || value == DBNull.Value) return 0;
      return Convert.ToInt32(value);
    }
    public static long GetAsLong(this DataRow row, string columnName) {
      var value = row[columnName];
      if (value == null || value == DBNull.Value) return 0;
      return Convert.ToInt64(value);
    }
    public static byte GetAsByte(this DataRow row, string columnName) {
      var value = row[columnName];
      if (value == null || value == DBNull.Value) return 0;
      return Convert.ToByte(value);
    }

    public static DataRow FindRow(this DataRowCollection rows, string column, string value) {
      foreach (DataRow row in rows)
        if (row.GetAsString(column) == value)
          return row;
      return null; 
    }


  }//class

}//namespace
