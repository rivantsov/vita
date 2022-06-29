using System.Configuration;
using MyEntityModel;
using Vita.Data;

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
      // MyDatabase must exist on local server; or adjust conn string
      var connString = ConfigurationManager.AppSettings["ConnString"]; 
      // Change to the driver for your server type if not MS SQL Server
      var driver = new Vita.Data.MsSql.MsSqlDbDriver();
      _dbSettings = new DbSettings(driver, Vita.Data.MsSql.MsSqlDbDriver.DefaultMsSqlDbOptions, connString);
      App = new MyEntityApp();
      App.LogPath = "_operationLog.log"; // in bin folder
      App.ConnectTo(_dbSettings);
      App.Init();
    }

  }
}
