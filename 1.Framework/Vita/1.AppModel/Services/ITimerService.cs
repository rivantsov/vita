using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Services.Implementations;

namespace Vita.Entities.Services {

  public enum TimerInterval {
    T_100_Ms,
    T_500_Ms,
    T_1_Sec,
    T_5_Sec,
    T_15_Sec,
    T_1_Min,
    T_5_Min,
    T_15_Min,
    T_60_Min,
  }

  public interface ITimerService : IEntityServiceBase {
    void Subscribe(TimerInterval interval, Action handler);
    void UnSubscribe(TimerInterval interval, Action handler);
  }

  public interface ITimerServiceControl {
    void EnableTimers(bool enable);
    void Fire(TimerInterval interval);
    void FireAll(); 
  }

}
