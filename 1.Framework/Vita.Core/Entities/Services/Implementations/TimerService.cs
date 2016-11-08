using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Services.Implementations {

  public class TimerService : ITimerService, ITimerServiceControl {
    // We need to have a single global instance of TimerService
    public static TimerService Instance {
      get {
        _instance = _instance ?? new TimerService();
        return _instance; 
      }
    } static TimerService _instance;
    private TimerService() { }

    private Timer _timer;
    private ITimeService _timeService;
    private bool _enabled; 
    private IErrorLogService _errorLog; 
    int _lastSeconds;
    object _lock = new object();

    public event EventHandler Elapsed100Ms;

    public event EventHandler Elapsed1Second;

    public event EventHandler Elapsed10Seconds;

    public event EventHandler Elapsed1Minute;
    public event EventHandler Elapsed5Minutes;

    bool _initialized; 
    public void Init(EntityApp app) {
      if(_initialized)
        return; 
      _errorLog = app.GetService<IErrorLogService>();
      _timeService = TimeService.Instance;
      _timer = new Timer(Timer_Elapsed, null, 100, 100);
      app.AppEvents.Initializing += Events_Initializing;
      _initialized = true; 
    }

    void Events_Initializing(object sender, AppInitEventArgs e) {
      if(e.Step == EntityAppInitStep.Initialized) {
        _enabled = true; 
      }
    }

    void Timer_Elapsed(object state) {
      if(!_enabled)
        return; 
      lock(_lock) {
        if(Elapsed100Ms != null)
          SafeInvoke(Elapsed100Ms.GetInvocationList());
        var utcNow = _timeService.UtcNow;
        var seconds = (int) utcNow.TimeOfDay.TotalSeconds;
        if(seconds == _lastSeconds) 
          return; 
        _lastSeconds = seconds;
        if(Elapsed1Second != null)
          SafeInvoke(Elapsed1Second.GetInvocationList());
        if(seconds % 10 == 0 && Elapsed10Seconds != null)
          SafeInvoke(Elapsed10Seconds.GetInvocationList());
        if(seconds % 60 == 0 && Elapsed1Minute != null)
          SafeInvoke(Elapsed1Minute.GetInvocationList());
        if(seconds % 300 == 0 && Elapsed5Minutes != null)
          SafeInvoke(Elapsed5Minutes.GetInvocationList());
      }
    }

    private void SafeInvoke(Delegate[] delegates) {
      foreach(var d in delegates) {
        var evh = (EventHandler)d;
        try {
          evh(this, EventArgs.Empty);
        } catch(Exception ex) {
          _errorLog.LogError(ex);
        }
      }
    }

    public void Shutdown() {
      _enabled = false;  
    }

    public void EnableAutoFire(bool enable) {
      _enabled = enable; 
    }

    //ITimerServiceControl
    public void FireAll() {
      lock(_lock) {
        try {
          if(Elapsed100Ms != null)
            Elapsed100Ms(this, EventArgs.Empty);
          if(Elapsed1Second != null)
            Elapsed1Second(this, EventArgs.Empty);
          if(Elapsed10Seconds != null)
            Elapsed10Seconds(this, EventArgs.Empty);
          if(Elapsed1Minute != null)
            Elapsed1Minute(this, EventArgs.Empty);
          if(Elapsed5Minutes != null)
            Elapsed5Minutes(this, EventArgs.Empty);
        } catch(Exception ex) {
          if(_errorLog != null)
            _errorLog.LogError(ex);
        }
      }

    }
  }
}
