using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.Logging {

  //Entities
  [Entity, OrderBy("CreatedOn:DESC"), ClusteredIndex("CreatedOn,Id"), DoNotTrack]
  public interface IErrorLog : ILogEntityBase {
    DateTime LocalTime { get; set; }

    [Size(50), Nullable]
    string MachineName { get; set; }

    [Size(50), Nullable, Index]
    string AppName { get; set; }
    [Size(250)]
    string Message { get; set; }
    [Size(100), Index]
    string ExceptionType { get; set; }
    [Unlimited, Nullable]
    string Details { get; set; }
    [Unlimited, Nullable]
    string OperationLog { get; set; }
    bool IsClientError { get; set; }

  }

}
  