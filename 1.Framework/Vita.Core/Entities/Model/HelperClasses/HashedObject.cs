using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;

namespace Vita.Entities.Model {

  //Base for objects that can be used as keys in dictionaries or hashes
  public abstract class HashedObject {
    private int _hashCode;

    public HashedObject() {
      _hashCode = Util.NewHash();
    }
    public override int GetHashCode() {
      return _hashCode;
    }
  }


}
