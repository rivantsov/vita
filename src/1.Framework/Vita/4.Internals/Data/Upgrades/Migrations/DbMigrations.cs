using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Data.Driver;
using Vita.Entities;

namespace Vita.Data.Upgrades {

  public enum DbMigrationTiming {
    Start,
    Middlle,
    End,
  }

  public abstract class DbMigration {
    static int _creationCount;
    public readonly EntityModule Module;
    public readonly Version Version;
    public string Name;
    public string Description; 
    internal int CreationOrder; //used for keeping 'natural' (creation order) to later use it when applying scripts

    public DbMigration(EntityModule module, string version, string name, string description) {
      Module = module; 
      Version = new Version(version);
      Name = name;
      Description = description;
      CreationOrder = System.Threading.Interlocked.Increment(ref _creationCount);
    }

    public override string ToString() {
      return Name;
    }
    public static int Compare(DbMigration x, DbMigration y) {
      var result = x.Module.Name.CompareTo(y.Module.Name);
      if (result != 0) return result;
      result = x.Version.CompareTo(y.Version);
      if (result != 0) return result;
      result = x.CreationOrder.CompareTo(y.CreationOrder);
      return result; 
    }

  }

  public class SqlMigration : DbMigration {
    public readonly DbMigrationTiming Timing;
    public string Sql;
    public SqlMigration(EntityModule module, string version, string name, string description, string sql, DbMigrationTiming timing )
        : base(module, version, name, description) {
      Sql = sql;
      Timing = timing; 
    }

    public DbScriptType GetDdlScriptType() {
      switch(Timing) {
        case DbMigrationTiming.Start: return DbScriptType.MigrationStartUpgrade;
        case DbMigrationTiming.Middlle: return DbScriptType.MigrationMidUpgrade;
        case DbMigrationTiming.End: 
        default:
          return DbScriptType.MigrationEndUpgrade;
      }

    }
  }//class

  public class ActionMigration : DbMigration {
    public Action<IEntitySession> Action;
    public ActionMigration(EntityModule module, string version, string name, string description, Action<IEntitySession> action)  : base(module, version, name, description) {
      Action = action;
    }
  }



}
