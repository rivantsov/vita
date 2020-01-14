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
    const double BaseTimerIntervalMs = 100.0;
    private System.Timers.Timer _timer;
    private bool _enabled;
    private ILogService _log;
    List<TimerSubscriptionInfo> _subscriptions = new List<TimerSubscriptionInfo>();
    long _tickCount;

    class TimerSubscriptionInfo {
      public TimerInterval Interval; 
      public int BaseCycleCount;
      public IList<Action> Subscribers = new List<Action>();

      // access to these methods is protected by lock, so we don't lock inside
      public void Subscribe(Action action) {
        var newList = new List<Action>(Subscribers);
        newList.Add(action);
        Subscribers = newList; 
      }
      
      public void Unsubscribe(Action action) {
        var newList = new List<Action>(Subscribers);
        if (newList.Contains(action))
          newList.Remove(action);
        Subscribers = newList;
      }
    }// class

    public TimerService() {
      CreateVirtualTimers(); 
    }
 
    public void Init(EntityApp app) {
      _log = app.GetService<ILogService>();
      app.AppEvents.Initializing += Events_Initializing;
      _timer = new System.Timers.Timer(BaseTimerIntervalMs);
      _timer.Elapsed += Timer_Elapsed;
      _timer.Enabled = true;
    }

    void Events_Initializing(object sender, AppInitEventArgs e) {
      if(e.Step == EntityAppInitStep.Initialized) {
        _enabled = true;
      }
      // We init tick count from UtcTime, 
      //  in order to make sure that long-period events fire consistently, independent of this service restarts.
      //  So that if this service restarts at 1:05:00, the next 15-minute timer will still fire at 1:15:00, not at 1:20 
      var utcNow = TimeService.Instance.UtcNow;
      _tickCount = (long)utcNow.TimeOfDay.TotalMilliseconds / 100;
    }

    public void Shutdown() {
      _enabled = false;
    }

    #region ITimerService implementation

    object _subscriptionLock = new object();

    public void Subscribe(TimerInterval interval, Action handler) {
      lock(_subscriptionLock) {
        var subscr = GetSubscription(interval);
        subscr?.Subscribe(handler);
      }
    }
    public void UnSubscribe(TimerInterval interval, Action handler) {
      lock(_subscriptionLock) {
        var subscr = GetSubscription(interval);
        subscr.Unsubscribe(handler);
      }
    } //method
    #endregion


    #region ITimerServiceControl
    public void EnableTimers(bool enable) {
      _enabled = enable;
      _log.WriteMessage($"TimersService: Enabled status changed to {_enabled}");
    }

    public void Fire(TimerInterval interval) {
      var timer = GetSubscription(interval);
      SafeInvoke(timer); 
    }

    public void FireAll() {
      foreach(var subscr in _subscriptions)
        SafeInvoke(subscr);
    }
    #endregion

    private TimerSubscriptionInfo GetSubscription(TimerInterval interval) {
      return _subscriptions.First(tr => tr.Interval == interval);
    }

    private void CreateVirtualTimers() {
      _subscriptions.Clear(); 
      AddSubscriptionInfo(TimerInterval.T_100_Ms, 1);
      AddSubscriptionInfo(TimerInterval.T_500_Ms, 5);
      AddSubscriptionInfo(TimerInterval.T_1_Sec, 10);
      AddSubscriptionInfo(TimerInterval.T_5_Sec, 10 * 5);
      AddSubscriptionInfo(TimerInterval.T_15_Sec, 10 * 15);
      AddSubscriptionInfo(TimerInterval.T_1_Min, 10 * 60);
      AddSubscriptionInfo(TimerInterval.T_5_Min, 10 * 60 * 5);
      AddSubscriptionInfo(TimerInterval.T_15_Min, 10 * 60 * 15);
      AddSubscriptionInfo(TimerInterval.T_60_Min, 10 * 60 * 60);
    }

    private TimerSubscriptionInfo AddSubscriptionInfo(TimerInterval interval, int baseCycleCount) {
      var subscr = new TimerSubscriptionInfo() { Interval = interval, BaseCycleCount = baseCycleCount };
      _subscriptions.Add(subscr);
      return subscr; 
    }

    private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
      if(!_enabled)
        return;
      _tickCount++;
      foreach(var subscr in _subscriptions) {
        if(_tickCount % subscr.BaseCycleCount == 0 && subscr.Subscribers.Count > 0)
          SafeInvoke(subscr);
      }
    }

    private void SafeInvoke(TimerSubscriptionInfo subscr) {
      foreach(var action in subscr.Subscribers) {
        try {
          action();
        } catch(Exception ex) {
          _log.LogError(ex);
        }
      }
    }

  }
}
