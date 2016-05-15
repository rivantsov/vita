using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Modules.Logging {

  public static class IncidentTypes {

    public const string DbConnectFailed = "DbConnectFailed";
    public const string DbDeadlock = "DeadLock";
  }

  [Entity, DoNotTrack]
  public interface IIncidentLog : ILogEntityBase {

    [Size(30), Index]
    string Type { get; set; }
    [Size(30), Nullable]
    string SubType { get; set; }

    [Size(Sizes.Description)]
    string Message { get; set; }

    [Index]
    Guid? KeyId1 { get; set; }
    [Index]
    Guid? KeyId2 { get; set; }


    [Size(Sizes.Name), Nullable, Index]
    string Key1 { get; set; } 
    [Size(Sizes.Name), Nullable, Index]
    string Key2 { get; set; }
    [Size(Sizes.LongName), Nullable, Index]
    string LongKey3 { get; set; }
    [Size(Sizes.LongName), Nullable, Index]
    string LongKey4 { get; set; }

    [Unlimited, Nullable]
    string Notes { get; set; }

    [Nullable]
    IIncidentAlert Alert { get; set; }

  }

  //Represents an alert fired based on incident log (ex: 3 or more failed logins within 1 minute)
  [Entity, DoNotTrack]
  public interface IIncidentAlert : ILogEntityBase {

    [Size(30), Index]
    string AlertType { get; set; }

    [Size(30), Index]
    string IncidentType { get; set; }

    IList<IIncidentLog> Incidents { get; }

  }


}
