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

    public IDictionary<Type, LogEntry[]> EntriesByType {
      get {
        if(_entriesByType == null)
          _entriesByType = Entries.GroupBy(e => e.GetType()).ToDictionary(g => g.Key, g => g.ToArray());
        return _entriesByType; 
      }
    }  IDictionary<Type, LogEntry[]> _entriesByType;


    public IList<LogEntry> GetEntries(Type entryType) {
      if(EntriesByType.TryGetValue(entryType, out var entries))
        return entries;
      return _emptyEntryList; 
    }
  } //class

  public interface ILogBatchingService: IObserver<LogEntry>, IObservable<LogEntryBatch> {
    
  }

  public interface ILogBatchListener {
    void SaveBatch(LogEntryBatch batch);
  }


}
