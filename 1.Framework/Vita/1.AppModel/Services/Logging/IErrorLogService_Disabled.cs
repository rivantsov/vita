using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Entities.Services {

  public interface IErrorLogService {
    Guid LogError(Exception exception, OperationContext context = null);
    Guid LogError(string message, string details, OperationContext context = null, DateTime? localTime = null, Guid? errorId = null);
    event EventHandler<AppErrorEventArgs> Error;
  }

}
