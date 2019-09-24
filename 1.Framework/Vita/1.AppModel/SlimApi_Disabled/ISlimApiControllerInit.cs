using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Api {
  
  public interface ISlimApiControllerInit {
    void InitController(OperationContext context);
  }

}//ns
