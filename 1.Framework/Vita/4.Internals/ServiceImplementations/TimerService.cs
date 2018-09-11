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
    private Timer _timer;
    private ITimeService _timeService;
    private bool _enabled;
    private ILogService _errorLog;
    long _lastTickCount;
    object _lock = new object();

    public event EventHandler Elapsed100Ms;
    public event EventHandler Elapsed500Ms;

    public event EventHandler Elapsed1Second;

    public event EventHandler Elapsed10Seconds;

    public event EventHandler Elapsed1Minute;
    public event EventHandler Elapsed5Minutes;
    public event EventHandler Elapsed15Minutes;
    public event EventHandler Elapsed30Minutes;
    public event EventHandler Elapsed60Minutes;
    public event EventHandler Elapsed6Hours;
    public event EventHandler Elapsed24Hours;

    public TimerService() {
    }

    public void Init(EntityApp app) {
      _errorLog = app.GetService<ILogService>();
      _timeService = TimeService.Instance;
      _timer = new Timer(Timer_Elapsed, null, 100, 100);
      app.AppEvents.Initializing += Events_Initializing;
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
        var utcNow =  _timeService.UtcNow;
        var tickCount = (long)utcNow.TimeOfDay.TotalMilliseconds /100;
        if(tickCount == _lastTickCount)
          return;
        _lastTickCount = tickCount; 
        if(Elapsed500Ms != null && (tickCount % 5 == 0))
          SafeInvoke(Elapsed500Ms.GetInvocationList());
        // Now all in 1 second intervals
        var seconds = tickCount / 10;
        if(Elapsed1Second != null)
          SafeInvoke(Elapsed1Second.GetInvocationList());
        if(seconds % 10 == 0 && Elapsed10Seconds != null)
          SafeInvoke(Elapsed10Seconds.GetInvocationList());
        if(seconds % 60 == 0 && Elapsed1Minute != null)
          SafeInvoke(Elapsed1Minute.GetInvocationList());
        var oneMin = 60;
        if(seconds % (5 * oneMin) == 0 && Elapsed5Minutes != null)
          SafeInvoke(Elapsed5Minutes.GetInvocationList());
        if(seconds % (15 * oneMin) == 0 && Elapsed15Minutes != null)
          SafeInvoke(Elapsed15Minutes.GetInvocationList());
        if(seconds % (30 * oneMin) == 0 && Elapsed30Minutes != null)
          SafeInvoke(Elapsed30Minutes.GetInvocationList());
        if(seconds % (60 * oneMin) == 0 && Elapsed60Minutes != null)
          SafeInvoke(Elapsed60Minutes.GetInvocationList());
        if(seconds % (60 * 6 * oneMin) == 0 && Elapsed6Hours != null)
          SafeInvoke(Elapsed6Hours.GetInvocationList());
        if(seconds % (60 * 24 * oneMin) == 0 && Elapsed24Hours != null)
          SafeInvoke(Elapsed24Hours.GetInvocationList());
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
          Elapsed100Ms?.Invoke(this, EventArgs.Empty);
          Elapsed1Second?.Invoke(this, EventArgs.Empty);
          Elapsed10Seconds?.Invoke(this, EventArgs.Empty);
          Elapsed1Minute?.Invoke(this, EventArgs.Empty);
          Elapsed5Minutes?.Invoke(this, EventArgs.Empty);
          Elapsed15Minutes?.Invoke(this, EventArgs.Empty);
          Elapsed15Minutes?.Invoke(this, EventArgs.Empty);
          Elapsed30Minutes?.Invoke(this, EventArgs.Empty);
          Elapsed60Minutes?.Invoke(this, EventArgs.Empty);
          Elapsed6Hours?.Invoke(this, EventArgs.Empty);
          Elapsed24Hours?.Invoke(this, EventArgs.Empty);
        } catch(Exception ex) {
          if(_errorLog != null)
            _errorLog.LogError(ex);
        }
      }

    }
  }
}
