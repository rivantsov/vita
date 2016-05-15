using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services;
using Vita.Data;
using Vita.Modules.Logging;

namespace Vita.Modules.DbInfo {

  [Entity, DoNotTrack]
  public interface IDbInfo {
    [PrimaryKey, Auto]
    Guid Id { get; }

    [Size(Sizes.Name)]
    string AppName { get; set; }

    // Instance type cannot be changed from the client code, should be changed directly in the database by admin.
    // It identifies database instance as dev, staging (shared pre-prod), or production
    [NoUpdate]
    DbInstanceType InstanceType { get; }

    // Major.Minor.Build : 1.3.25
    [Size("AppVersion")]
    string Version { get; set; }

    bool LastModelUpdateFailed { get; set; }

    [Unlimited, Nullable]
    string LastModelUpdateError { get; set; }

    [Unlimited, Nullable]
    string Values { get; set; }
  }

  [Entity, DoNotTrack]
  public interface IDbModuleInfo {
    [PrimaryKey, Auto]
    Guid Id { get; }

    [Size(Sizes.Name)]
    string ModuleName { get; set; }
    [Size(Sizes.Name)]
    string Schema { get; set; }
    // Major.Minor.Build : 1.3.25
    [Size("AppVersion")]
    string Version { get; set; }
  }
}
