using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.DataHistory {

  public enum HistoryAction {
    Created,
    Updated,
    Deleted,
  }

  [Entity, BypassAuthorization, OrderBy("CreatedOn:DESC")]
  [Display("{EntityName}/{EntityPrimaryKey}")]
  public interface IDataHistory {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Auto(AutoType.CreatedOn), Utc, Index]
    DateTime CreatedOn { get; }

    [Auto(AutoType.CreatedById)]
    Guid CreatedByUserId { get; }

    Guid? TransactionId { get; set; }

    HistoryAction Action { get; set; }

    [Size(50)]
    string EntityName { get; set; }
    [HashFor("EntityName"), Index]
    int EntityNameHash { get; set; }
    [Size(100)]
    string EntityPrimaryKey { get; set; }
    [HashFor("EntityPrimaryKey"), Index]
    int EntityPrimaryKeyHash { get; set; }
    [Unlimited]
    string EntityData { get; set; }
  }

}//ns
