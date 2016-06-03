using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Modules.DataHistory {

  public class DataHistoryModule : EntityModule, IDataHistoryService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    HashSet<Type> _trackedEntities = new HashSet<Type>();

    public DataHistoryModule(EntityArea area) : base(area, "DataHistory", version: CurrentVersion) {
      this.RegisterEntities(typeof(IDataHistory));
      App.RegisterService<IDataHistoryService>(this); 
    }

    public override void Init() {
      base.Init(); 
      // hook to Saving events
      foreach(var type in _trackedEntities) {
        var entInfo = App.Model.GetEntityInfo(type);
        Util.Check(entInfo != null, "DataHistoryModule: Type {0} registered for history tracking is not an entity.", type);
        entInfo.SaveEvents.SavingChanges += SaveEvents_SavingChanges;
      }
    }

    private void SaveEvents_SavingChanges(Entities.Runtime.EntityRecord record, EventArgs args) {
      record.CreateHistoryEntry();
    }

    #region IDataHistoryService members
    public ICollection<Type> GetTrackedEntities() {
      return _trackedEntities.ToList();
    }
    public void TrackHistoryFor(params Type[] entityTypes) {
      Util.Check(this.App.Status == EntityAppStatus.Initializing,
        "DataHistoryModule: entities may not be registered for history tracking after application was initialized.");
      _trackedEntities.UnionWith(entityTypes);
    }

    public IList<DataHistoryEntry> GetEntityHistory(IEntitySession session, Type entityType, object primaryKey, 
                DateTime? fromDate = null, DateTime? tillDate = null, int skip = 0, int take = 100, Guid? userId = null) {
      var entInfo = session.Context.App.Model.GetEntityInfo(entityType);
      Util.Check(entInfo != null, "Type {0} is not registered as an entity.", entityType);
      var entName = entInfo.FullName;
      var entNameHash = Util.StableHash(entName);
      var where = session.NewPredicate<IDataHistory>()
        .And(h => h.EntityNameHash == entNameHash && h.EntityName == entName)
        .AndIfNotEmpty(fromDate, h => h.CreatedOn > fromDate.Value)
        .AndIfNotEmpty(tillDate, h => h.CreatedOn < tillDate.Value);
      if (primaryKey != null) {
        var pkStr = PrimaryKeyAsString(primaryKey);
        var pkHash = Util.StableHash(pkStr);
        where = where.And(h => h.EntityPrimaryKeyHash == pkHash && h.EntityPrimaryKey == pkStr);
      }
      var query = session.EntitySet<IDataHistory>().Where(where).OrderByDescending(h => h.CreatedOn).Skip(skip).Take(take);
      var histEntList = query.ToList();
      var result = histEntList.Select(h => h.ToHistoryEntry(entInfo)).ToList();
      return result; 
    }

    public DataHistoryEntry GetEntityOnDate(IEntitySession session, Type entityType, object primaryKey, DateTime onDate) {
      Util.Check(primaryKey != null, "Primary key may not be null");
      var entInfo = session.Context.App.Model.GetEntityInfo(entityType);
      Util.Check(entInfo != null, "Type {0} is not registered as an entity.", entityType);
      var entName = entInfo.FullName;
      var entNameHash = Util.StableHash(entName);
      string pkStr = PrimaryKeyAsString(primaryKey);
      var pkHash = Util.StableHash(pkStr);
      var query = session.EntitySet<IDataHistory>().Where(h => h.EntityNameHash == entNameHash && h.EntityName == entName &&
         h.EntityPrimaryKeyHash == pkHash && h.EntityPrimaryKey == pkStr && h.CreatedOn < onDate)
         .OrderByDescending(h => h.CreatedOn);
      var histEnt = query.FirstOrDefault();
      var result = histEnt.ToHistoryEntry(entInfo);
      return result;
    }

    private string PrimaryKeyAsString(object primaryKey) {
      if(primaryKey is EntityKey)
        return ((EntityKey)primaryKey).ValuesToString();
      else
        return primaryKey.ToString();
    }
    #endregion


  }//class
}//ns
