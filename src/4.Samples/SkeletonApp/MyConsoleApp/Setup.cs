using System.Configuration;
using System.IO;
using MyEntityModel;
using Vita.Data;
using Microsoft.Extensions.Configuration;
using System.Reflection;

/*
Sample entity app setup and data access code. Do not include this class into entity model library, where you define
entities and entity module and entity app. The entity model is intended to be driver/database kind agnostic. 
Put this initialization code into the hosting app. Add reference to one of the VITA driver packages 
(ex: Vita.Data.MsSql).
The database MyDatabase must exist before you run this code, create it manually

*/

namespace MyConsoleApp
{
  public static class Setup
  {
    public static MyEntityApp App;
    static DbSettings _dbSettings;

    public static void Init()
    {
      var appConfig = new ConfigurationBuilder().AddJsonFile("appSettings.json").Build();

      // MyDatabase must exist on local server; or adjust conn string
      var connString = appConfig["SQLiteConnectionString"];

      // adjust conn string
      if (connString.Contains("{bin}")) {
        var asmPath = Assembly.GetExecutingAssembly().Location;
        var binFolder = Path.GetDirectoryName(asmPath);
        connString = connString.Replace("{bin}", binFolder);
      }

      // Change to the driver for your server type if not MS SQL Server
      var driver = new Vita.Data.SQLite.SQLiteDbDriver();
      _dbSettings = new DbSettings(driver, Vita.Data.SQLite.SQLiteDbDriver.DefaultSQLiteDbOptions, connString);
      App = new MyEntityApp();
      App.LogPath = "_operationLog.log"; // in bin folder
      App.ConnectTo(_dbSettings);
      App.Init();
    }

  }
}
