using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Services;

namespace Vita.Modules.Logging {

  public interface ITransactionLogService {
    // TODO: fill out TransactionLogService methods
  }

  public class TransactionLogModule : EntityModule, ITransactionLogService, IObjectSaveHandler {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    public TransactionLogSettings Settings;

    #region TransactionLogEntry nested class
    //Temp object used to store trans information in the background update queue
    public class TransactionLogEntry : LogEntry {
      public int Duration;

      public int RecordCount;
      public string Changes;

      public TransactionLogEntry(OperationContext context, DateTime startedOn, int duration, int recordCount, string changes)
              : base(context, startedOn) {
        Duration = duration;
        RecordCount = recordCount;
        Changes = changes; 
      }

    }//class
    #endregion

    IBackgroundSaveService _saveService;
    bool _trackHostApp;

    public TransactionLogModule(EntityArea area, TransactionLogSettings settings = null, bool trackHostApp = true) : base(area, "TransactionLog", version: CurrentVersion) {
      Settings = settings ?? new TransactionLogSettings();
      _trackHostApp = trackHostApp; 
      App.RegisterConfig(Settings); 
      RegisterEntities(typeof(ITransactionLog));
      App.RegisterService<ITransactionLogService>(this);
    }

    public override void Init() {
      base.Init();
      _saveService = App.GetService<IBackgroundSaveService>();
      _saveService.RegisterObjectHandler(typeof(TransactionLogEntry), this);
      if(_trackHostApp)
        SetupUpdateLoggingFor(this.App);
    }

    public void SetupUpdateLoggingFor(EntityApp targetApp) {
      targetApp.AppEvents.SavedChanges += Events_SavedChanges;
      targetApp.AppEvents.ExecutedNonQuery += AppEvents_ExecutedNonQuery;
    }

    #region IObjectSaveHandler members
    //Called on background thread to persist the transaction data
    public void SaveObjects(IEntitySession session, IList<object> items) {
      foreach (TransactionLogEntry entry in items) {
        var entTrans = session.NewLogEntity<ITransactionLog>(entry);
        entTrans.Duration = entry.Duration;
        entTrans.RecordCount = entry.RecordCount;
        entTrans.Changes = entry.Changes;
      }
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
      if(Settings.IgnoreAreas.Contains(ent.Area.Name))
        return false;
      return true; 
    }

  }
}
