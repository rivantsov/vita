using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Services;

namespace Vita.Modules.Logging {

  public interface ITransactionLogService {
    void SetupLoggingFor(EntityApp app);
  }

  public class TransactionLogModule : EntityModule, ITransactionLogService {
    public static readonly Version CurrentVersion = new Version("1.1.0.0");
    public TransactionLogSettings Settings;



    public TransactionLogModule(EntityArea area, TransactionLogSettings settings = null, bool trackHostApp = true) : base(area, "TransactionLog", version: CurrentVersion) {
      Settings = settings ?? new TransactionLogSettings();
      App.RegisterConfig(Settings); 
      RegisterEntities(typeof(ITransactionLog));
      App.RegisterService<ITransactionLogService>(this);
      if(trackHostApp)
        SetupLoggingFor(this.App);
    }

    public override void Init() {
      base.Init();
    }

    #region ITransactionLogService members
    public void SetupLoggingFor(EntityApp targetApp) {
      targetApp.AppEvents.SavedChanges += Events_SavedChanges;
      targetApp.AppEvents.ExecutedNonQuery += AppEvents_ExecutedNonQuery;
    }
    #endregion 

    void AppEvents_ExecutedNonQuery(object sender, EntitySessionEventArgs e) {
      // TODO: finish this, for now not sure what and how to do logging here
    }

    void Events_SavedChanges(object sender, EntitySessionEventArgs e) {
      var entSession = (EntitySession)e.Session;
      var dur = (int)(App.TimeService.ElapsedMilliseconds - entSession.TransactionStart);
      //Filter out entities/records that we do not need to track
      var recChanged = entSession.RecordsChanged;
      if(recChanged.Count == 0)
        return;
      var filteredRecs = recChanged.Where(r => ShouldTrack(r)).ToList(); 
      if(filteredRecs.Count == 0)
        return; 
      string changes = BuildChangeLog(filteredRecs);
      var user = entSession.Context.User;
      var userSession = entSession.Context.UserSession;
      var transEntry = new TransactionLogEntry(entSession.Context, entSession.TransactionDateTime, dur, entSession.TransactionRecordCount, changes);
      _saveService.AddObject(transEntry);
    }

    private string BuildChangeLog(IList<EntityRecord> records) {
      var sb = new StringBuilder();
      foreach(var rec in records) {
        sb.Append(rec.EntityInfo.FullName);
        sb.Append("/");
        sb.Append(rec.StatusBeforeSave.ToString());
        sb.Append("/");
        sb.Append(rec.PrimaryKey.ValuesToString());
        sb.Append(";;");
      }
      return sb.ToString();
    }
  
    private bool ShouldTrack(EntityRecord record) {
      var ent = record.EntityInfo;
      if(ent.Flags.IsSet(EntityFlags.DoNotTrack))
        return false;
      if(Settings.IgnoreSchemas.Contains(ent.Area.Name))
        return false;
      return true; 
    }

  }
}
