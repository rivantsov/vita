using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Entities.DbInfo {

  public enum DbInstanceType {
    // Dev instance on dev machine, not shared, allow schema update any time directly from the app
    Development = 0,
    // Shared test/staging version; software installed through centralized deployment process
    Staging = 1,
    // Production database, schema update allowed only through db tool by system admin
    Production = 2,
  }


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
    [Size(20)]
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
    [Size(20)]
    string Version { get; set; }
  }
}
