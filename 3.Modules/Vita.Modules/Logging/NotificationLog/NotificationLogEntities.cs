using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Logging {

  [Entity]
  public interface INotificationLog : ILogEntityBase {
    [Size(100), Nullable]
    string Type { get; set; }

    [Size(20)]
    string MediaType { get; set; }
    [Size(50)]
    string Status { get; set; }

    int AttemptCount { get; set; }

    [Size(100)]
    string MainRecipient { get; set; } //main recipient
    Guid? MainRecipientUserId { get; set; }
    [Nullable, Unlimited]
    string Recipients { get; set; }

    [Unlimited, Nullable]
    string Parameters { get; set; }

    [Unlimited, Nullable]
    string Error { get; set; }

    [Unlimited, Nullable]
    string AttachmentList { get; set; }
  }

}
