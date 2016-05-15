using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities;
using Vita.Modules.DbInfo; 

namespace Vita.Modules.Logging {


  public enum DbModelChangeStatus {
    Incomplete = 0,
    OK = 1,
  }


  [Entity, DoNotTrack, OldNames("DbModelChangeBatch")]
  public interface IDbUpgradeBatch {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Auto(AutoType.CreatedOn)]

    DateTime StartedOn { get; set; }
    
    DateTime? CompletedOn { get; set; }

    [Size(30)]
    string FromVersion { get; set; }

    [Size(30)]
    string ToVersion { get; set; }


    bool Success { get; set; }
    DbUpgradeMethod Method { get; set; }
    [Size(50)]
    string UserName { get; set; }
    [Size(50)]
    string MachineName { get; set; }

    [Unlimited, Nullable]
    string Notes { get; set; }

    [Unlimited, Nullable]
    string Errors { get; set; }

  }

  [Entity, DoNotTrack, OldNames("DbModelChangeScript")]
  public interface IDbUpgradeScript {
    [PrimaryKey, Auto]
    Guid Id { get; }

    [OldNames("ChangeBatch,Batch")]
    IDbUpgradeBatch Batch { get; set; }

    [Auto(AutoType.CreatedOn)]
    DateTime? StartedOn { get; }

    DbObjectChangeType ChangeType { get; set; }
    DbObjectType ObjectType { get; set; }
    
    [Size(100)]
    string FullObjectName { get; set; }

    int ExecutionOrder { get; set; }
    int SubOrder { get; set; }
    int Duration { get; set; }

    [Unlimited]
    string Sql { get; set; }

    [Unlimited, Nullable]
    string Errors { get; set; }
  }


}
