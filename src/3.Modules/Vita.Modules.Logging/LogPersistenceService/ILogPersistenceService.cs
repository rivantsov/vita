﻿using System;
using System.Collections.Generic;

using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging {

  public class LogEntryBatch {
    private static List<LogEntry> _emptyEntryList = new List<LogEntry>();

    public IEntitySession Session; 
    public IList<LogEntry> Entries;

    public IDictionary<Type, LogEntry[]> EntriesByType;
    public object CustomData;

    public IList<LogEntry> GetEntries(Type entryType) {
      if(EntriesByType.TryGetValue(entryType, out var entries))
        return entries;
      return _emptyEntryList; 
    }
  } //class


  public interface ILogPersistenceService : IObserver<IList<LogEntry>>, IObservable<LogEntryBatch> {

  }

}
