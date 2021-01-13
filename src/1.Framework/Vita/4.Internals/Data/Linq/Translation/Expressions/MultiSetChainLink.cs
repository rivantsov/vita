using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Data.Linq.Translation.Expressions {

  public enum MultiSetType {
    Union,
    UnionAll,
    Intersect,
    Except,
  }

  // Link to next set in UNION, EXCEPT, INTERSECT operations
  public class MultiSetChainLink {
    public SelectExpression ChainedSelect;
    public MultiSetType MultiSetType; 
  }
}
