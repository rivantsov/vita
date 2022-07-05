using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Entities.Model.Construction {
  // key expansion methods
  public partial class EntityModelBuilder {

    private void ExpandEntityKeyMembersNew() {
      var allKeys = Model.Entities.SelectMany(e => e.Keys).ToList(); 


      CheckErrors();
    }

    private bool TryExpandKey(EntityKeyInfo key) {
      
    }

  }
}
