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
      _hashCode = NewHash();
    }
    public override int GetHashCode() {
      return _hashCode;
    }

    private static int _hashCount;
    // artificial randomized hash value
    public static int NewHash() {
      _hashCount++;
      var sHash = _hashCount + "_" + _hashCount;
      var code = sHash.GetHashCode();
      return code;
    }


  }


}
