using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Services.Implementations {

  public class TimerService : ITimerService, ITimerServiceControl {
    OperationContext _timerContext;
    private Timer _timer;
    private IOperationLogService _logService; 
    int _elapseCount;
    object _lock = new object();

    public event EventHandler Elapsed100Ms;

    public event EventHandler Elapsed1Second;

    public event EventHandler Elapsed10Seconds;

    public event EventHandler Elapsed1Minute;
    public event EventHandler Elapsed5Minutes;

    public TimerService() {

    }

    public void Init(EntityApp app) {
      _timerContext = app.CreateSystemContext(); 
      _logService = app.GetService<IOperationLogService>(); 
      _timer = new Timer(100);
      _timer.Elapsed += Timer_Elapsed;
      app.AppEvents.Initializing += Events_Initializing;
    }

    void Events_Initializing(object sender, AppInitEventArgs e) {
      if(e.Step == EntityAppInitStep.Initialized) {
        _timer.Start(); 
      }
    }

    void Timer_Elapsed(object sender, ElapsedEventArgs e) {
      unchecked {
        _elapseCount++;
      }
      lock(_lock) {
        try {
          if(Elapsed100Ms != null)
            Elapsed100Ms(this, EventArgs.Empty);
          if (_elapseCount % 10 == 0 && Elapsed1Second != null)
            Elapsed1Second(this, EventArgs.Empty);
          if(_elapseCount % 100 == 0 && Elapsed10Seconds != null)
            Elapsed10Seconds(this, EventArgs.Empty);
          if(_elapseCount % 600 == 0 && Elapsed1Minute != null)
            Elapsed1Minute(this, EventArgs.Empty);
          if(_elapseCount % 3000 == 0 && Elapsed5Minutes != null)
            Elapsed5Minutes(this, EventArgs.Empty);
        } catch(Exception ex) {
          if(_logService != null)
            _logService.Log(new ErrorLogEntry(_timerContext, ex));
        }
      }
    }

    public void Shutdown() {
      _timer.Stop();      
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
          if(_logService != null)
            _logService.Log(new ErrorLogEntry(_timerContext, ex));
        }
      }

    }
  }
}
