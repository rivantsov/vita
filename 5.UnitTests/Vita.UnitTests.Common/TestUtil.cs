using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Services;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Upgrades;

namespace Vita.UnitTests.Common {

  public enum TestMethodKind {
    None, Init, Test, Cleanup
  }

  public static class TestUtil {

    public static TestMethodKind GetMethodKind(MethodInfo method) {
      var attrTypes = method.GetCustomAttributes().Select(a => a.GetType());
      var isDisabled = attrTypes.Any(t => t.Name == "IgnoreAttribute");
      if (isDisabled)
        return TestMethodKind.None;
      var isTest = attrTypes.Any(t => t.Name == "TestMethodAttribute" || t.Name == "TestAttribute");
      if (isTest)
        return TestMethodKind.Test;
      var isInit = attrTypes.Any(t => t.Name == "TestInitializeAttribute" || t.Name == "SetUpAttribute");
      if (isInit)
        return TestMethodKind.Init;
      var isCleanup = attrTypes.Any(t => t.Name == "TestCleanupAttribute");
      if (isCleanup)
        return TestMethodKind.Cleanup;
      return TestMethodKind.None;
    }//method

    public static bool IsTestClass(Type testClass) {
      var attrTypes = testClass.GetCustomAttributes().Select(a => a.GetType());
      var isTest = attrTypes.Any(t => t.Name == "TestClassAttribute" || t.Name == "TestFixtureAttribute");
      return isTest;
    }
  
    public static bool TryParseServerType(string str, out DbServerType serverType) {
      if (Enum.TryParse<DbServerType>(str, true, out serverType))
        return true;
      return false;
    }

    public static DbServerType[] ParseServerTypes(string str) {
      var strTypes = str.SplitNames();
      var types = new List<DbServerType>();
      for (int i = 0; i < strTypes.Length; i++) {
        DbServerType type;
        if (TryParseServerType(strTypes[i], out type))
          types.Add(type); 
      }
      return types.ToArray(); 
    }

    public static void DropSchemaObjects(DbSettings settings) {
      var driver = settings.ModelConfig.Driver; 
      var loader = driver.CreateDbModelLoader(settings, null);
      var model = loader.LoadModel();

      var upgradeInfo = new DbUpgradeInfo(null, model);
      foreach(var sch in model.Schemas)
        if (sch.Schema != "dbo") //just for MS SQL, can never drop this
          upgradeInfo.AddChange(sch, null);
      foreach(var tbl in model.Tables) {
        upgradeInfo.AddChange(tbl, null);
        foreach(var refc in tbl.RefConstraints)
          upgradeInfo.AddChange(refc, null);
        foreach(var key in tbl.Keys)
          if (key.KeyType.IsSet(KeyType.Index))
            upgradeInfo.AddChange(key, null);
      }
      foreach (var custType in model.CustomDbTypes)
        upgradeInfo.AddChange(custType, null);
      foreach(var cmd in model.Commands)
        upgradeInfo.AddChange(cmd, null);
      foreach (var seq in model.Sequences)
        upgradeInfo.AddChange(seq, null);

      var updater = driver.CreateDbModelUpdater(settings); 
      updater.BuildScripts(upgradeInfo);
      upgradeInfo.AllScripts.Sort(DbUpgradeScript.CompareExecutionOrder);
      //apply
      var conn = driver.CreateConnection(settings.SchemaManagementConnectionString);
      try {
        conn.Open();
        foreach(var script in upgradeInfo.AllScripts) {
          var cmd = conn.CreateCommand();
          cmd.CommandText = script.Sql;
          driver.ExecuteCommand(cmd, DbExecutionType.NonQuery); 
        }
      } catch(Exception ex) {
        var logStr = ex.ToLogString();
        System.Diagnostics.Debug.WriteLine(logStr); 
        throw; 
      } finally {
        conn.Close();
      }
    }


    public static void DeleteAllData(EntityApp app, IEnumerable<Type> inEntities = null, IEnumerable<Type> exceptEntities = null, 
         IList<string> extraTablesToDelete = null) {
      var db = app.GetDefaultDatabase();
      if (inEntities == null)
        inEntities = app.GetAllEntityTypes();
      var typesToClear = new HashSet<Type>(inEntities);
      if(exceptEntities != null)
        typesToClear.ExceptWith(exceptEntities); 
      // get existing tables; in unit tests it might happen that when we delete all for table that does not exist yet
      var modelLoader = db.DbModel.Driver.CreateDbModelLoader(db.Settings, null);
      var oldModel = modelLoader.LoadModel(); 
      var oldtableNames = new HashSet<string>(oldModel.Tables.Select(t => t.FullName), StringComparer.OrdinalIgnoreCase);
      //Figure out table/entity list and sort it
      var modelInfo = db.DbModel.EntityApp.Model;
      var entList = new List<EntityInfo>();
      foreach (var entType in typesToClear) {
        var entityInfo = modelInfo.GetEntityInfo(entType);
        if (entityInfo == null) continue;
        entList.Add(entityInfo);
      }
      //sort in topological order
      entList.Sort((x, y) => x.TopologicalIndex.CompareTo(y.TopologicalIndex));
      // delete all one-by-one
      var conn = db.DbModel.Driver.CreateConnection(db.ConnectionString);
      conn.Open();
      var cmd = conn.CreateCommand();
      cmd.Transaction = conn.BeginTransaction();
      try {
        if (extraTablesToDelete != null)
          foreach(var extraTable in extraTablesToDelete) {
            if (oldtableNames.Contains(extraTable))
              DeleteData(cmd, extraTable);
          }
        foreach (var entityInfo in entList) {
          var table = db.DbModel.LookupDbObject<DbTableInfo>(entityInfo);
          if (table == null)
            continue;
          if (!oldtableNames.Contains(table.FullName))
            continue; 
          DeleteData(cmd, table.FullName); 
        }
      } finally {
        cmd.Transaction.Commit(); 
        conn.Close();
      }
    }

    private static void DeleteData(IDbCommand cmd, string tableName) {
      cmd.CommandText = StringHelper.SafeFormat("DELETE FROM {0};", tableName);
      cmd.ExecuteNonQuery();
    }

    public static T ExpectFailWith<T>(Action action) where T : Exception {
      try {
        action();
      } catch(Exception ex) {
        if(ex is T)
          return (T) ex;
        throw; 
      }
      Util.Throw("Exception {0} not thrown.", typeof(T));
      return null;
    }

    public static ClientFaultException ExpectClientFault(Action action) {
      return ExpectFailWith<ClientFaultException>(action);
    }
    public static DataAccessException ExpectDataAccessException(Action action) {
      return ExpectFailWith<DataAccessException>(action);
    }

    public static bool EqualsTo(this DateTime x, DateTime y, int precisionMs = 1) {
      return x <= y.AddMilliseconds(precisionMs) && x > y.AddMilliseconds(-precisionMs); 
    }

    /// <summary> Compares arrays of bytes. One use is for comparing row version properties (which are of type byte[]). </summary>
    /// <param name="byteArray">Value to compare.</param>
    /// <param name="other">Value to compare with.</param>
    /// <returns>True if array lengths and byte values match. Otherwise, false.</returns>
    public static bool EqualsTo(this byte[] byteArray, byte[] other) {
      if(byteArray == null && other == null) return true;
      if(byteArray == null || other == null) return false;
      if(byteArray.Length != other.Length)
        return false;
      for(int i = 0; i < byteArray.Length; i++)
        if(byteArray[i] != other[i])
          return false;
      return true;
    }

    public static Database GetDefaultDatabase(this EntityApp app) {
      var ds = app.GetDefaultDataSource();
      return ds.Database;
    }

    public static DataSource GetDefaultDataSource(this EntityApp app) {
      return app.DataAccess.GetDataSources().First();
    }



  }
}
