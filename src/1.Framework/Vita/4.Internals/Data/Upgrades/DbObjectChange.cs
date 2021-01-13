using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Entities;

namespace Vita.Data.Upgrades {


  public class DbObjectChange {
    public DbModelObjectBase OldObject;
    public DbModelObjectBase NewObject;
    public string Notes;

    public DbObjectChangeType ChangeType;
    public DbObjectType ObjectType;
    public List<string> Errors = new List<string>();

    public List<DbUpgradeScript> Scripts = new List<DbUpgradeScript>();


    public DbObjectChange(DbModelObjectBase oldObject, DbModelObjectBase newObject,
                         DbObjectChangeType? changeType = null, string notes = null) {
      var obj = newObject ?? oldObject;
      Util.Check(obj != null, "Fatal error in DbModelChange constructor: oldObject and newObject are both null.");
      ObjectType = obj.ObjectType;
      OldObject = oldObject;
      NewObject = newObject;
      Notes = notes;
      if(changeType == null)
        ChangeType = newObject == null ? DbObjectChangeType.Drop : (oldObject == null ? DbObjectChangeType.Add : DbObjectChangeType.Modify);
      else
        ChangeType = changeType.Value;
    }

    public override string ToString() {
      return Util.SafeFormat("{0} {1}: {2}", ChangeType, ObjectType, DbObject);
    }
    public DbModelObjectBase DbObject {
      get { return NewObject ?? OldObject; }
    }
    public DbUpgradeScript AddScript(DbScriptType scriptType, string sqlTemplate, params object[] args) {
      return AddScript(scriptType, ApplyPhase.Default, sqlTemplate, args);
    }

    public DbUpgradeScript AddScript(DbScriptType scriptType, ApplyPhase phase, string sqlTemplate, params object[] args) {
      Util.CheckNotEmpty(sqlTemplate, "Fatal error: attempt to add empty upgrade SQL scrpt; scriptType: {0}", scriptType);
      string sql = sqlTemplate;
      if(args != null && args.Length > 0)
        sql = string.Format(sqlTemplate, args);
      var script = new DbUpgradeScript(scriptType, sql, this, phase);
      Scripts.Add(script);
      return script;
    }
    public void NotSupported(string message, params object[] args) {
      var msg = Util.SafeFormat(message, args);
      Errors.Add(msg);
    }
  }

  public class DbTableChangeGroup {
    public DbTableInfo OldTable;
    public DbTableInfo NewTable;
    public List<DbObjectChange> Changes = new List<DbObjectChange>();
    public string TableName;

    public DbTableChangeGroup(DbTableInfo oldTable, DbTableInfo newTable) {
      OldTable = oldTable;
      NewTable = newTable;
      var tbl = NewTable ?? OldTable;
      TableName = tbl.FullName;
    }
    internal DbObjectChange AddChange(DbModelObjectBase oldObj, DbModelObjectBase newObj, DbObjectChangeType? changeType = null, string notes = null) {
      var change = new DbObjectChange(oldObj, newObj, changeType, notes);
      this.Changes.Add(change);
      return change;
    }

    public override string ToString() {
      return "TableChangeGroup: " + TableName;
    }
  }//class


}
