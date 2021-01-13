using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Model.Construction {

  public interface IEntityClassProvider {
    void SetupEntityClasses(EntityModel model); 
  }

}
