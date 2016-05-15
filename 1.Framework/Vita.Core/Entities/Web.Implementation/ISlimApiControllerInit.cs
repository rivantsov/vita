using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Web.Implementation {
  
  public interface ISlimApiControllerInit {
    void InitController(OperationContext context);
  }

}//ns
