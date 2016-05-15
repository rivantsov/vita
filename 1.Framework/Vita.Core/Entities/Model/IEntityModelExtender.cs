using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Model {

  /// <summary>Interface for entity model extenders. </summary>
  /// <remarks>TransactionLog module implements an extender that adds transaction-tracking properties (transaction_id) to entities. </remarks>
  public interface IEntityModelExtender {
    void Extend(EntityModel model);
  }

}
