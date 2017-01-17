using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Services.Implementations;

namespace Vita.Entities.Services {
  public interface ITimerService : IEntityService {
    //timer events
    event EventHandler Elapsed100Ms;
    event EventHandler Elapsed1Second;
    event EventHandler Elapsed10Seconds;
    event EventHandler Elapsed1Minute;
    event EventHandler Elapsed5Minutes;
    event EventHandler Elapsed15Minutes;
    event EventHandler Elapsed30Minutes;
    event EventHandler Elapsed60Minutes;
    event EventHandler Elapsed6Hours;
    event EventHandler Elapsed24Hours;
  }

  public interface ITimerServiceControl {
    void EnableAutoFire(bool enable); 
    void FireAll(); 
  }

}
