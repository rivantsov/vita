using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Model.Construction;

namespace Vita.Entities.Services {

  /// <summary>Service for entity model customization. </summary>
  /// <remarks>TransactionLog module uses this facility to add transaction-tracking properties (transaction_id) to entities.
  /// Note that service implementation does not actually modify the model - it is just a container for requested changes. 
  /// The customizations are done by the <see cref="EntityModelBuilder">.</remarks>
  public interface IEntityModelCustomizationService {
    void AddMember(Type entityType, string name, Type memberType, int size = 0, bool nullable = false, Attribute[] attributes = null);
    void AddIndex(Type entityType, IndexAttribute index);
    void ReplaceEntity(Type entityType, Type withEntityType);
    void RegisterSize(string code, int size, EntityModule module = null);
    void MoveTo(EntityArea toArea, params Type[] entityTypes);
  }

}
