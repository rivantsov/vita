using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 

namespace Vita.Modules.Logging {

  [Entity]
  public interface IEvent {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Size(Sizes.Name)]
    string EventType { get; set; }
    [HashFor("EventType"), Index]
    int EventTypeHash { get; }

    [Utc, Index]
    DateTime StartedOn { get; set; }
    int Duration { get; set; }

    [Nullable, Size(100)]
    string Location { get; set; }

    //Free-form values, 'main' value for easier search - rather than putting in parameters
    [Index]
    double? Value { get; set; }
    [Nullable, Size(50)]
    string StringValue { get; set; }

    Guid? UserId { get; set; }
    Guid? TenantId { get; set; }
    Guid? SessionId { get; set; }

    IList<IEventParameter> Parameters { get; }
  }

  [Entity, Unique("Event,Name")]
  public interface IEventParameter {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [CascadeDelete]
    IEvent Event { get; set; }
    [Size(Sizes.Name)]
    string Name { get; set; }
    [Nullable, Unlimited]
    string Value { get; set; }
  }
}
