using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Data;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Data.Runtime;

namespace Vita.Data.Upgrades {

  public class DbMigrationSet  {
    EntityApp _app;
    Database _database;
    DbModel _oldDbModel; 

    internal readonly List<SqlMigration> SqlMigrations = new List<SqlMigration>();
    internal readonly List<ActionMigration> ActionMigrations = new List<ActionMigration>(); 
    internal EntityModule CurrentModule;

    public DbMigrationSet(EntityApp app, Database database, DbModel oldModel) {
      _app = app;
      _database = database;
      _oldDbModel = oldModel; 
    }

    // Add migration methods
    public DbMigration AddSql(string version, string name, string description, string sql, DbMigrationTiming timing = DbMigrationTiming.End) {
      var migr = new SqlMigration(CurrentModule, version, name, description, sql, timing);
      SqlMigrations.Add(migr);
      return migr;
    }

    public DbMigration AddPostUpgradeAction(string version, string name, string description, Action<IEntitySession> action) {
      var migr = new ActionMigration(CurrentModule, version, name, description, action);
      ActionMigrations.Add(migr);
      return migr;
    }


    #region public utility methods to use in migrations
    public DbServerType ServerType {
      get { return _database.DbModel.Driver.ServerType; }
    }
    /// <summary>Returns current DB version; might be lower than app version before upgrade is completed. </summary>
    public Version DbVersion {
      get { return _oldDbModel.VersionInfo.Version; }
    }
    public DbModel DbModel {
      get { return _database.DbModel; }
    }
    public DbModel OldDbModel {
      get { return _oldDbModel; }
    }
    public string GetFullTableName<TEntity>() {
      return _database.DbModel.GetTable(typeof(TEntity)).FullName; 
    }

    public Version GetModuleDbVersion(EntityModule module) {
      if (_oldDbModel.VersionInfo == null)
        return DbVersionInfo.ZeroVersion;
      var schema = DbModel.Config.GetSchema(module.Area); 
      var mi = _oldDbModel.VersionInfo.GetModule(schema, module.Name);
      if (mi == null)
        return DbVersionInfo.ZeroVersion;
      return mi.Version; 
    }

    public bool IsNewInstall() {
      return _oldDbModel.VersionInfo == null;
    }

    public DbUpgradeSettings GetDbUpgradeSettings() => _database.Settings.UpgradeSettings;
    #endregion

    internal IList<SqlMigration> GetActiveSqlMigrations() {
      var list = SqlMigrations.Where(m => IsActive(m)).ToList();
      list.Sort(DbMigration.Compare);
      return list; 
    }
    internal IList<ActionMigration> GetActiveActionMigrations() {
      var list = ActionMigrations.Where(m => IsActive(m)).ToList();
      list.Sort(DbMigration.Compare);
      return list; 
    }

    public bool IsActive(DbMigration migration) {
      var dbVersion = GetModuleDbVersion(migration.Module);
      var currCodeVersion = migration.Module.Version; 
      return migration.Version > dbVersion && migration.Version <= currCodeVersion;
    }

    public void IgnoreDbObjectChanges(string objectName) {
      _database.Settings.IgnoreDbObjectChanges(objectName); 
    }
  }
}
