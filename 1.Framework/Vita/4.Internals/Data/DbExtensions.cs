using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

using Vita.Entities;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Model;
using Vita.Data.Runtime;
using Vita.Entities.Runtime;
using Vita.Data.Linq;

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

    public static bool IsSet(this DbConnectionFlags flags, DbConnectionFlags flag) {
      return (flags & flag) != 0;
    }


    public static DataSource GetDefaultDataSource(this EntityApp app) {
      var da = app.GetService<IDataAccessService>();
      return da.GetDataSources().FirstOrDefault(); 
    }


    public static bool CheckConnectivity(this DbSettings dbSettings, bool rethrow = false) {
      try {
        CheckConnectivity(dbSettings.Driver, dbSettings.ConnectionString);
        if(dbSettings.SchemaManagementConnectionString != dbSettings.ConnectionString)
          CheckConnectivity(dbSettings.Driver, dbSettings.SchemaManagementConnectionString);
        return true;
      } catch(Exception) {
        if(rethrow)
          throw;
        else 
          return false;
      } 
    }

    private static void CheckConnectivity(DbDriver driver, string connectionString) {
      IDbConnection conn = null;
      try {
        conn = driver.CreateConnection(connectionString);
        conn.Open();
      } finally {
        if(conn != null)
          conn.Close();
      }

    }

    public static DbType GetIntDbType(this Type type) {
      switch(type.Name) {
        case "Int32":
          return DbType.Int32;
        case "UInt32":
          return DbType.UInt32;
        case "Int64":
          return DbType.Int64;
        case "UInt64":
          return DbType.UInt64;
        default:
          Util.Throw($"Function Unsupported for type {type}, must be int32 or int64.");
          return DbType.Object; //never happens
      }
    }


  }//class

}//namespace
