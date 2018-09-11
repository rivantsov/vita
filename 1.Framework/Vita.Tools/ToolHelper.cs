using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Vita.Data;
using Vita.Data.Driver;
using Vita.Entities;
using Vita.Tools.DbFirst;

namespace Vita.Tools {
  public static class ToolHelper {

    public static DbDriver CreateDriver(DbServerType serverType) {
      var className = GetDriverClassName(serverType);
      var type = Type.GetType(className);
      if (type == null) {
        var asm = TryLoadingDriverAssembly(className);
        type = asm.GetType(className);
      }
      Util.Check(type != null, "Failed to locate driver class {0}; load (instantiate) driver explicitly.", className);
      var driver = (DbDriver)Activator.CreateInstance(type);
      return driver; 
    }

    private static Assembly TryLoadingDriverAssembly(string className) {
      var asmName = string.Join(".", className.Split('.').Take(3)) + ".dll";
      var asm = Assembly.LoadFrom(asmName);
      Util.Check(asm != null, "Failed to load assembly {0} for class {1}; make sure the assembly is in the bin folder," + 
                               " or reference it and load the class explicitly.", asmName, className);
      return asm; 
    }

    public static string GetDriverClassName(DbServerType serverType) {
      switch(serverType) {
        case DbServerType.MsSql:
          return "Vita.Data.MsSql.MsSqlDbDriver";
        case DbServerType.MySql:
          return "Vita.Data.MySql.MySqlDbDriver";
        case DbServerType.Postgres:
          return "Vita.Data.Postgres.PgDbDriver";
        case DbServerType.SQLite:
          return "Vita.Data.SQLite.SQLiteDbDriver";
        default:
          Util.Throw("Unknown driver type: {0}.", serverType);
          return null; 
      }
    } //method

    public static DbSettings CreateDbSettings(DbServerType serverType, string connectionString) {
      var driver = CreateDriver(serverType);
      var options = driver.GetDefaultOptions();
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

    public static string GetValue(this XmlNode xmlNode, string name, string defaultValue = null) {
     // if(!name.StartsWith("//"))
       // name = "//" + name;
      foreach(var ch in xmlNode.ChildNodes) {
        var el = ch as XmlElement;
        if(el != null && el.Name == name)
          return el.InnerText.Trim(); 
      }
      return defaultValue; 
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
