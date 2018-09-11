using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Model.Construction {

  public interface IEntityClassProvider {
    void SetupEntityClasses(EntityModel model); 
  }

  // Used when we do not actually need provider (ex: ToolLib when constructing entity model from db)
  public class DummyEntityClassProvider : IEntityClassProvider {
    public void SetupEntityClasses(EntityModel model) {  }
  }
}
