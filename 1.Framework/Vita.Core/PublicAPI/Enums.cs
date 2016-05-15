using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities {

  /// <summary>Determines if DB connection should be kept open in EntitySession. </summary>
  public enum DbConnectionReuseMode {
    /// <summary>Do not reuse connection, close it immediately after operation completes. </summary>
    NoReuse,
    /// <summary>
    /// Do not close connection, wait for next operation. Best for Web applications. 
    /// At the end of web request processing all open connections will be closed anyway. 
    /// </summary>
    KeepOpen,
  }

  /// <summary>Cache type, identifies type of cache that should be used for an entity. </summary>
  public enum CacheType {
    None,
    LocalSparse,
    Sparse,
    FullSet,
  }

  [Flags]
  public enum DbViewOptions {
    None = 0,
    Materialized = 1 << 1,
/*  Not supported yet
    Insert = 1 << 2,
    Update = 1 << 3,
    Delete = 1 << 4,
 */ 
  }

}
