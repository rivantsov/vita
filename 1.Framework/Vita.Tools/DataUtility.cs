using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Runtime;
using Vita.Data.Upgrades;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Tools.DbFirst;

namespace Vita.Tools {

  public static class DataUtility {

    public static DbDriver CreateDriver(DbServerType serverType) {
      var className = GetDriverClassName(serverType);
      var type = Type.GetType(className);
      if(type == null) {
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
        case DbServerType.Oracle:
          return "Vita.Data.Oracle.OracleDbDriver";
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

    public static DbModel BuildDbModel(EntityApp app, DbSettings dbSettings) {
      if(app.Status == EntityAppStatus.Created)
        app.Init();
      var dbmBuilder = new DbModelBuilder(app.Model, dbSettings.ModelConfig, app.ActivationLog);
      var dbModel = dbmBuilder.Build();
      return dbModel;
    }

    public static DbModel LoadDbModel(EntityApp app, DbSettings dbSettings) {
      var schemas = app.Areas.Select(a => a.Name).ToList(); 
      return LoadDbModel(dbSettings, schemas, app.ActivationLog);
    }

    public static DbModel LoadDbModel(DbSettings settings, List<string> schemas, ILog log) {
      var driver = settings.ModelConfig.Driver;
      var loader = driver.CreateDbModelLoader(settings, log);
      loader.SetSchemasSubset(schemas); 
      var model = loader.LoadModel();
      return model;
    }


    public static void DropSchemaObjects(EntityApp app, DbSettings dbSettings) {
      var schemas = app.Areas.Select(a => a.Name).ToList();
      DropSchemaObjects(dbSettings, schemas, app.ActivationLog);
    }

    public static void DropSchemaObjects(DbSettings settings, List<string> schemas, ILog log) {
      var driver = settings.Driver; 
      var model = LoadDbModel(settings, schemas, log);
      var upgradeInfo = new DbUpgradeInfo(null, model);
      foreach(var sch in model.Schemas)
        if(sch.Schema != "dbo") //just for MS SQL, can never drop this
          upgradeInfo.AddChange(sch, null);
      foreach(var tbl in model.Tables) {
        upgradeInfo.AddChange(tbl, null);
        foreach(var refc in tbl.RefConstraints)
          upgradeInfo.AddChange(refc, null);
        foreach(var key in tbl.Keys)
          if(key.KeyType.IsSet(KeyType.Index))
            upgradeInfo.AddChange(key, null);
      }
      //  foreach (var custType in model.CustomDbTypes)
      //    upgradeInfo.AddChange(custType, null);
      foreach(var seq in model.Sequences)
        upgradeInfo.AddChange(seq, null);

      var updater = driver.CreateDbModelUpdater(settings);
      updater.BuildScripts(upgradeInfo);
      upgradeInfo.AllScripts.Sort(DbUpgradeScript.CompareExecutionOrder);
      //apply
      var conn = driver.CreateConnection(settings.SchemaManagementConnectionString);
      IDbCommand currCmd;
      DbUpgradeScript currScript = null;
      try {
        conn.Open();
        foreach(var script in upgradeInfo.AllScripts) {
          currScript = script;
          currCmd = conn.CreateCommand();
          currCmd.CommandText = script.Sql;
          driver.ExecuteCommand(currCmd, DbExecutionType.NonQuery);
        }
      } catch(Exception ex) {
        var logStr = ex.ToLogString();
        System.Diagnostics.Debug.WriteLine(logStr);
        var allScripts = string.Join(Environment.NewLine, upgradeInfo.AllScripts);
        Debug.WriteLine("SCRIPTS: \r\n" + allScripts);
        if(currScript != null)
          Debug.WriteLine("Failed script: " + currScript);
        throw;
      } finally {
        conn.Close();
      }
    }

    public static void DropTablesSafe(DbSettings dbSettings, string schema, params string[] tables) {
      var driver = dbSettings.Driver;
      var conn = driver.CreateConnection(dbSettings.SchemaManagementConnectionString);
      conn.Open();
      try {
        foreach(var table in tables) {
          var fullName = driver.SqlDialect.FormatFullName(schema, table);
          var cmd = conn.CreateCommand();
          cmd.CommandText = $"DROP TABLE {fullName}";
          try {
            cmd.ExecuteNonQuery();
          } catch(Exception ex) {
            Debug.WriteLine($"Failed to drop table {fullName}: {ex.Message}");
          } 
        } //foreach
      } finally {
        conn.Close(); 
      }
    }

    public static void DeleteAllData(EntityApp app, IEnumerable<Type> inEntities = null, IEnumerable<Type> exceptEntities = null,
         IList<string> extraTablesToDelete = null) {
      Util.Check(app.Status == EntityAppStatus.Connected, "EntityApp must be connected to database.");
      var db = app.GetDefaultDatabase();
      var dbSettings = db.Settings;
      var driver = dbSettings.Driver;
      if(inEntities == null)
        inEntities = app.GetAllEntityTypes();
      var typesToClear = new HashSet<Type>(inEntities);
      if(exceptEntities != null)
        typesToClear.ExceptWith(exceptEntities);
      // get existing tables; in unit tests it might happen that when we delete all for table that does not exist yet
      var modelLoader = driver.CreateDbModelLoader(dbSettings, app.ActivationLog);
      var schemas = app.Areas.Select(a => a.Name).ToList(); 
      modelLoader.SetSchemasSubset(schemas);
      var oldModel = modelLoader.LoadModel();
      var oldtableNames = new HashSet<string>(oldModel.Tables.Select(t => t.FullName), StringComparer.OrdinalIgnoreCase);
      //Figure out table/entity list and sort it
      var modelInfo = app.Model;
      var entList = new List<EntityInfo>();
      foreach(var entType in typesToClear) {
        var entityInfo = modelInfo.GetEntityInfo(entType);
        if(entityInfo == null)
          continue;
        entList.Add(entityInfo);
      }
      //sort in topological order
      entList.Sort((x, y) => x.TopologicalIndex.CompareTo(y.TopologicalIndex));
      // delete all one-by-one
      var conn = driver.CreateConnection(dbSettings.ConnectionString);
      conn.Open();
      var cmd = conn.CreateCommand();
      cmd.Transaction = conn.BeginTransaction();
      try {
        if(extraTablesToDelete != null)
          foreach(var extraTable in extraTablesToDelete) {
            if(oldtableNames.Contains(extraTable))
              ExecuteDelete(driver, cmd, extraTable);
          }
        foreach(var entityInfo in entList) {
          var table = db.DbModel.LookupDbObject<DbTableInfo>(entityInfo);
          if(table == null)
            continue;
          if(!oldtableNames.Contains(table.FullName))
            continue;
          ExecuteDelete(driver, cmd, table.FullName);
        }
      } finally {
        cmd.Transaction.Commit();
        conn.Close();
      }
    }

    private static void ExecuteDelete(DbDriver driver, IDbCommand cmd, string tableName) {
      cmd.CommandText = Util.SafeFormat("DELETE FROM {0}", tableName); 
      driver.ExecuteCommand(cmd, DbExecutionType.NonQuery);
    }



  }
}
