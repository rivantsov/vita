using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging {

  public class LogEntryBatch {
    private static List<LogEntry> _emptyEntryList = new List<LogEntry>();

    public IEntitySession Session; 
    public IList<LogEntry> Entries;
    public object CustomData;

    public IDictionary<Type, List<LogEntry>> EntriesByType {
      get {
        if(_entriesByType == null)
          _entriesByType = Entries.GroupBy(e => e.GetType()).ToDictionary(g => g.Key, g => g.ToList());
        return _entriesByType; 
      }
    }  IDictionary<Type, List<LogEntry>> _entriesByType;

    public IList<LogEntry> GetEntries(Type entryType) {
      if(EntriesByType.TryGetValue(entryType, out var entries))
        return entries;
      return _emptyEntryList; 
    }
  } //class

  public interface ILogPersistenceService {
    void Push(LogEntry entry);
    void Flush();
    void RegisterHandler(ILogBatchListener handler);
  }

  public interface ILogBatchListener {
    void SaveBatch(LogEntryBatch batch);
  }


}
