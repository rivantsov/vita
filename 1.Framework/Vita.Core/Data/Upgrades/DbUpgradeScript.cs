using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Data.Upgrades {

  public enum ApplyPhase {
    Early,
    Default,
    Late,
  }

  public class DbUpgradeScript {
    static int _creationCount;

    public DbScriptType ScriptType;
    public ApplyPhase Phase;
    private int _creationOrder; //used for keeping 'natural' (creation order) to later use it when applying scripts

    public string Sql;
    public bool Applied;
    public int Duration; //ms
    public DateTime? AppliedOn;

    public DbObjectChange ModelChange;
    public SqlMigration Migration;

    public DbUpgradeScript(DbScriptType scriptType, string sql, DbObjectChange modelChange = null, ApplyPhase phase = ApplyPhase.Default) {
      ScriptType = scriptType;
      Sql = sql;
      ModelChange = modelChange;
      Phase = phase;
      _creationOrder = System.Threading.Interlocked.Increment(ref _creationCount);
    }

    public DbUpgradeScript(SqlMigration migration) {
      Migration = migration;
      ScriptType = Migration.GetDdlScriptType();
      Sql = migration.Sql; 
      _creationOrder = migration.CreationOrder; 
    }

    public override string ToString() {
      return ScriptType + ":" + ModelChange + Migration; //either model change or migration is there
    }

    //used in execution ordering
    public static int CompareExecutionOrder(DbUpgradeScript x, DbUpgradeScript y) {
      var result = x.ScriptType.CompareTo(y.ScriptType);
      if (result != 0) return result;
      result = x.Phase.CompareTo(y.Phase);
      if (result != 0) return result;
      result = x._creationOrder.CompareTo(y._creationOrder);
      return result;
    }
  }


}
