﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Modules.Logging {


  class LogPersistenceService : Observable<LogEntryBatch>, ILogPersistenceService, IEntityServiceBase {
    EntityApp _app; 

    public LogPersistenceService() {
      
    }

    public void Init(EntityApp app) {
      _app = app; 
    }
    public void Shutdown() {

    }

    public void OnCompleted() {
      base.BroadcastOnCompleted();
    }

    public void OnError(Exception error) {
      
    }

    public void OnNext(IList<LogEntry> entries) {
      try {
        var entriesByType = entries.GroupBy(e => e.GetType()).ToDictionary(g => g.Key, g => g.ToArray());
        var session = _app.OpenSystemSession();
        var batch = new LogEntryBatch() { Session = session, Entries = entries, EntriesByType = entriesByType };
        Broadcast(batch);
        session.SaveChanges();
      } catch (Exception ex) {
        _app.LogService.LogError(ex); 
      }
    }

  }
}
