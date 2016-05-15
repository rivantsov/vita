using System;
using System.Collections.Generic;
using System.Reflection;

namespace Vita.Common {

  public class StringSet : HashSet<String> {   
    public StringSet() : this(StringComparer.InvariantCultureIgnoreCase) {}
    public StringSet(IEqualityComparer<string> comparer) : base(comparer) { }
    public override string ToString() {
      return string.Join(" ", this); 
    }
  }

  public class StringList : List<String> {
    public override string ToString() {
      return string.Join(" ", this);
    }
  }

}
