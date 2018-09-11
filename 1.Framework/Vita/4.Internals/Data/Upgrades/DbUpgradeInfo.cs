using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Entities;

namespace Vita.Data.Upgrades {

  public class DbUpgradeInfo {
    public UpgradeStatus Status; 
    public Guid Id;
    public DbSettings Settings; 
    public DbModel NewDbModel;
    public DbModel OldDbModel;
    public List<DbObjectChange> NonTableChanges = new List<DbObjectChange>(); //schemas
    public List<DbTableChangeGroup> TableChanges = new List<DbTableChangeGroup>();
    public List<DbUpgradeScript> AllScripts = new List<DbUpgradeScript>();
    public List<ActionMigration> PostUpgradeMigrations;
    //the following are initialized automatically but are changed if we update using tool
    public DbUpgradeMethod Method;
    public bool VersionsChanged; //true if detected any version changes
    public string UserName;
    public DateTime StartedOn;
    public DateTime? EndedOn; 

    public DbUpgradeInfo(DbSettings settings, DbModel newModel) {
      Settings = settings;
      NewDbModel = newModel;
      var serverType = NewDbModel.Driver.ServerType;
      Id = Guid.NewGuid();
      Method = DbUpgradeMethod.Auto; //might be changed by update tool app
      UserName = "(app)";
      Status = UpgradeStatus.None; 
    }

    public void AddMigrations(DbMigrationSet migrations) {
      var migrScripts = migrations.GetActiveSqlMigrations().Select(m => new DbUpgradeScript(m));
      AllScripts.AddRange(migrScripts);
      PostUpgradeMigrations = migrations.GetActiveActionMigrations().ToList();
    }

    public override string ToString() {
      var totalCount = NonTableChanges.Count + TableChanges.Count;
      return Util.SafeFormat("{0}, {1} changes.", NewDbModel.EntityApp.AppName, totalCount);
    }
    public DbObjectChange AddChange(DbModelObjectBase oldObject, DbModelObjectBase newObject, string notes = null) {
      var change = new DbObjectChange(oldObject, newObject, null, notes);
      NonTableChanges.Add(change);
      return change;
    }

  }//class


}
