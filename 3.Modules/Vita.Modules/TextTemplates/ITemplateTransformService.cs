using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 

namespace Vita.Modules.TextTemplates {
  public interface ITemplateTransformService {
    ITextTemplate GetTemplate(IEntitySession session, string templateName, string culture = "EN-US", Guid? ownerId = null);
    string Transform(ITextTemplate template,  IDictionary<string, object> data);
  }
}
