using System;
using System.ComponentModel; 
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics; 
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging {

  public class OperationLogModule : EntityModule, IOperationLogService, IObjectSaveHandler {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    IBackgroundSaveService _saveService;


    public OperationLogModule(EntityArea area, LogLevel logLevel = LogLevel.Details) : base(area, "OperationLog", "Operation log module.", version: CurrentVersion) {
      this.LogLevel = logLevel;
      RegisterEntities(typeof(IOperationLog));
      App.RegisterService<IOperationLogService>(this);
    }

    public override void Init() {
      base.Init();
      _saveService = App.GetService<IBackgroundSaveService>();
      _saveService.RegisterObjectHandler(typeof(OperationLogEntry), this);
      _saveService.RegisterObjectHandler(typeof(SystemLogEntry), new SystemLogEntrySaveHandler());
      //Registering handler for LogEntry would cover all derived classes
    }

    #region IOperationLogService Members
    public LogLevel LogLevel { get; private set; }

    public void Log(OperationLogEntry entry) {
      _saveService.AddObject(entry);
    }

    public event EventHandler<LogSaveEventArgs> Saving;
    #endregion

    public void SaveObjects(IEntitySession session, IList<object> items) {
      //Group by WebCallId, SessionId, UserName
      var entries = items.OfType<LogEntry>().ToList();
      var groupedByWebCall = entries.GroupBy(e => e.WebCallId);
      foreach (var wg in groupedByWebCall) {
        if (wg.Key == null) {
          var groupedBySessionId = wg.GroupBy(e => e.UserSessionId);
          foreach (var sg in groupedBySessionId) {
            if (sg.Key == null) {
              var groupedByUserName = sg.GroupBy(e => e.UserName);
              foreach (var ug in groupedByUserName)
                SaveEntries(session, ug);
            } else
              SaveEntries(session, sg);
            }// foreach sg
          } //if wg.Key
          else 
            SaveEntries(session, wg);
      }//foreach wg
    }

    private void SaveEntries(IEntitySession session, IEnumerable<LogEntry> entries) {
      var ordered = entries.OrderBy(e => e.CreatedOn).ToList();
      if (ordered.Count == 0)
        return; 
      var text = string.Join(Environment.NewLine, ordered);
      if (Saving != null)
        Saving(this, new LogSaveEventArgs(text));
      var iLog = session.NewLogEntity<IOperationLog>(ordered[0]);
      iLog.Message = text; 
    }

    #region SystemLogEntrySaveHandler class
    //Handles saving system log entries - they are not grouped together, each gets its own record
    class SystemLogEntrySaveHandler : IObjectSaveHandler {

      public void SaveObjects(IEntitySession session, IList<object> items) {
        foreach (SystemLogEntry entry in items) {
          var iLog = session.NewLogEntity<IOperationLog>(entry);
          iLog.Message = entry.ToString();
        }
      }
    }
    #endregion

  }// class


}//ns
