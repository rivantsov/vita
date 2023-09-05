using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Vita.Entities {

  public enum EntitySessionOptions {
    None = 0,
    DisableLog = 1,
    DisableCache = 1 << 1,
    DisableBatchMode = 1 << 2,
    [Obsolete("This option is deprecated. Smart load is enabled by default. Use DisableSmartLoad option to disable it.")]
    EnableSmartLoad = 1 << 4,
    /// <summary>SmartLoad is a facility to automatically pre-load related records in advance for all loaded parent records. </summary>
    /// <remarks>For example, if we load a few IBook entities in a query, then iterate books and touch book.Publisher property,
    /// then the engine will lazy load not only this book&quot;s Publisher, but Publishers for all loaded books in the Entity session.</remarks>
    DisableSmartLoad = 1 << 5,
  }

  public enum EntityStatus {
    Stub, //an empty data record with only PK fields initialized
    Loading,  //in the process of loading from a data store
    Loaded,
    Modified,
    New,
    Deleting, //marked for deletion
    Fantom, //just deleted in database; or: created as new but then marked for deletion; so no action in database
  }



  public enum CommandSchedule {
    TransactionStart,
    TransactionEnd,
  }



}//ns