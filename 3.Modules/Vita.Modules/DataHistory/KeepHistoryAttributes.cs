using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Model.Construction;

namespace Vita.Modules.DataHistory {
  /// <summary>Specifies that full history of entity changes should be tracked in DataHistory module. </summary>
  [AttributeUsage(AttributeTargets.Interface)]
  public class KeepHistoryAttribute: EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      var srv = context.Model.App.GetService<IDataHistoryService>();
      if(srv != null)
        srv.RegisterToKeepHistoryData(entity.EntityType);
    }
  }//class

}
