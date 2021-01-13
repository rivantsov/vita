
using System;

namespace Vita.Data.Linq.Translation.Expressions {

  [Flags]
  public enum TableJoinType {
    Inner = 0,
    LeftOuter = 1,
    RightOuter = 1 << 1,
    FullOuter = LeftOuter | RightOuter,
    Default = Inner,
  }
}