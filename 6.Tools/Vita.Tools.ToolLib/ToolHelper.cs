using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Vita.Common;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Tools.DbFirst;

namespace Vita.Tools {
  public static class ToolHelper {

    public static DbDriver CreateDriver(DbServerType serverType, string connectionString = null) {
      return CreateDriver(serverType.ToString(), connectionString);
    }

    public static DbDriver CreateDriver(string driverCode, string connectionString = null) {
      Util.Check(!string.IsNullOrEmpty(driverCode), "Driver/provider code may not be empty.");
      switch(driverCode.ToLowerInvariant()) {
        case "mssql" :
          if(string.IsNullOrEmpty(connectionString))
            return new Vita.Data.MsSql.MsSqlDbDriver(Vita.Data.MsSql.MsSqlVersion.V2012);
          //Autodetect version
          return Vita.Data.MsSql.MsSqlDbDriver.Create(connectionString); 
        case "sqlce":
          return new Vita.Data.SqlCe.SqlCeDbDriver(); 
        case "mysql":
          return new Vita.Data.MySql.MySqlDbDriver(); 
        case "postgres":
          return new Vita.Data.Postgres.PgDbDriver();
        case "sqlite":
          return new Vita.Data.SQLite.SQLiteDbDriver();
        default:
          Util.Throw("Unknown driver code: {0}.", driverCode);
          return null; 
      }
    } //method

    public static DbOptions GetDefaultOptions(DbServerType serverType) {
      switch(serverType) {
        case DbServerType.MsSql: return Data.MsSql.MsSqlDbDriver.DefaultMsSqlDbOptions;
        case DbServerType.SqlCe: return Vita.Data.SqlCe.SqlCeDbDriver.DefaultDbOptions;
        case DbServerType.MySql: return Vita.Data.MySql.MySqlDbDriver.DefaultMySqlDbOptions;
        case DbServerType.Postgres: return Vita.Data.Postgres.PgDbDriver.DefaultPgDbOptions;
        case DbServerType.Sqlite: return Vita.Data.SQLite.SQLiteDbDriver.DefaultSQLiteDbOptions;
        default: return DbOptions.Default; 
      }      
    }

    public static DbSettings CreateDbSettings(DbServerType serverType, string connectionString = null) {
      var driver = CreateDriver(serverType, connectionString);
      var options = GetDefaultOptions(serverType);
      var dbStt = new DbSettings(driver, options, connectionString);
      return dbStt; 
    }

    public static bool TestConnection(DbDriver driver, string connString, out string message) {
      message = null; 
      var conn = driver.CreateConnection(connString);
      try {
        conn.Open();
        conn.Close();
        return true;
      } catch(Exception ex) {
        message = " Connection test failed: " + ex.Message;
        return false;
      }
    }//method

    public static string GetValue(this XmlNode xmlNode, string path, string defaultValue = null) {
      if(!path.StartsWith("//"))
        path = "//" + path;
      var node = xmlNode.SelectSingleNode(path);
      if(node == null)
        return defaultValue;
      return node.InnerText.Trim();
    }

    public static List<string> GetValueList(this XmlNode xmlNode, string path) {
      var options = xmlNode.GetValue(path).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).ToList();
      return options;
    }


    public static bool IsSet(this DbFirstOptions options, DbFirstOptions option) {
      return (options & option) != 0;
    }


  }
}
