using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Vita.Entities {

  public enum EntitySessionOptions {
    None = 0,
    DisableLog = 1,
    DisableCache = 1 << 1,
    DisableBatchMode = 1 << 2,
    EnableSmartLoad = 1 << 4,
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