using System;
using System.Collections.Generic;
using System.Reflection;

namespace Vita.Entities.Utilities {

  public class StringSet : HashSet<String> {   
    public StringSet() : this(StringComparer.OrdinalIgnoreCase) {}
    public StringSet(IEqualityComparer<string> comparer) : base(comparer) { }
    public override string ToString() {
      return string.Join(" ", this); 
    }
  }


}
