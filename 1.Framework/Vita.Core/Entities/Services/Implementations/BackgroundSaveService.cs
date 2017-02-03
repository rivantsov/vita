using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Runtime;

namespace Vita.Entities.Services.Implementations {

  public class BackgroundSaveService : IBackgroundSaveService, IEntityService {
    EntityApp _app;
    ITimerService _timerService;
    IErrorLogService _errorLog;
    IOperationLogService _operationLog; 
    ConcurrentQueue<object> _buffer = new ConcurrentQueue<object>();
    Dictionary<Type, IObjectSaveHandler> _handlers = new Dictionary<Type, IObjectSaveHandler>();
    bool _suspended; 
    object _lock = new object();

    public BackgroundSaveService() {

    }

    #region IBackgroundSaveService members
    public void RegisterObjectHandler(Type objectType, IObjectSaveHandler saver) {
      lock (_lock) {
        _handlers[objectType] = saver; 
      }
    }
    public void AddObject(object item) {
      _buffer.Enqueue(item);
    }

    public event EventHandler<BackgroundSaveEventArgs> Saving;

    public IDisposable Suspend() {
      return new SuspendToken(this); 
    }
    #endregion

    #region SuspendToken nested class
    class SuspendToken : IDisposable {
      BackgroundSaveService _service;
      internal SuspendToken(BackgroundSaveService service) {
        _service = service;
        _service._suspended = true; 
      }

      public void Dispose() {
        _service._suspended = false; 
      }
    }
    #endregion

    void TimerService_Elapsed1Second(object sender, EventArgs e) {
      if (_suspended)
        return; 
      if(_buffer.Count == 0  || _flushing)
        return;
      // do the job asynchronously, to avoid blocking timer
      Task.Run(() => Flush());
    }

    void Events_FlushRequested(object sender, EventArgs e) {
      Flush();
    }

    #region IEntityService members
    public void Init(EntityApp app) {
      _app = app;
      _errorLog = _app.GetService<IErrorLogService>();
      _operationLog = _app.GetService<IOperationLogService>(); 
      _timerService = _app.GetService<ITimerService>();
      _timerService.Elapsed1Second += TimerService_Elapsed1Second;
      _app.AppEvents.FlushRequested += Events_FlushRequested;
    }

    public void Shutdown() {
      Flush(); 
    }
    #endregion

    bool _flushing;
    private void Flush() {
      if(_flushing)
        return;
      if (!_app.IsConnected())
        return; 
      lock(_lock) {
        try {
          _flushing = true;
          var start = _app.TimeService.ElapsedMilliseconds; 
          var list = DequeuAll();
          if(list.Count == 0)
            return;
          //fire event
          Saving?.Invoke(this, new BackgroundSaveEventArgs(list));
          var session = (EntitySession) _app.OpenSystemSession();
          session.LogDisabled = true; //do not log operations; what we save are log entries themselves most of the time
          if (list.Count > 0) {
            var byHandler = list.GroupBy(o => GetHandler(o));
            foreach (var group in byHandler) {
              var handler = group.Key;
              if (handler == null)
                continue; 
              handler.SaveObjects(session, group.ToList());
            }
          }
          //Get stats
          var recCount = session.GetChangeCount(); 
          var startSaving = _app.TimeService.ElapsedMilliseconds;
          session.SaveChanges();
          var endSaving = _app.TimeService.ElapsedMilliseconds;
          //log stats
          if (_operationLog != null && OkToLogStatsMessage(list)) {
            var logEntry = new Logging.InfoLogEntry(session.Context,  "Background save completed, records: {0}, save time: {1} ms, total time: {2} ms.",
                recCount, (endSaving - startSaving), (endSaving - start));
            _operationLog.Log(logEntry);
          }
        } catch(Exception ex) {
          //System.Diagnostics.Debugger.Break();
          _errorLog.LogError(ex);
        } finally {
          _flushing = false; 
        }
      }
    }//method

    //Do not log stat if the only message we saved was the stats message from previous background save
    private bool OkToLogStatsMessage(IList<object> entries) {
      if(_app.Status != EntityAppStatus.Connected) //do not log if we are in shutdown
        return false; 
      if (entries == null || entries.Count == 0)
        return false;
      if (entries.Count > 1)
        return true;
      //we have single log message
      var msg = entries[0]  + string.Empty;
      if (msg.StartsWith("Background save completed"))
        return false;
      return true; 
    }

    private IObjectSaveHandler GetHandler(object item) {
      if(item == null)
        return null;
      var handler = item as IObjectSaveHandler;
      if(handler != null)
        return handler;
      handler = GetHandlerByType(item.GetType());
      if (handler != null)
        return handler;
      return null; 
    }
    private IObjectSaveHandler GetHandlerByType(Type type) {
      IObjectSaveHandler handler;
      //check type itself and all base types
      while (type != typeof(object)) {
        if (_handlers.TryGetValue(type, out handler))
          return handler;
        type = type.BaseType;
      }
      return null;
    }

    IList<object> DequeuAll() {
      var list = new List<object>(); 
      object item; 
      while(_buffer.TryDequeue(out item))
        list.Add(item);
      return list; 
    }

  }
}
