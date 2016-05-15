using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.TextTemplates {
  public static class TemplateExtensions {
    public static ITextTemplate NewTextTemplate(this IEntitySession session, string name, string template, TemplateFormat format, 
                  string culture = "EN-US", string engine = "Simple", Guid? ownerId = null) {
      var templ = session.NewEntity<ITextTemplate>();
      templ.Name = name;
      templ.Template = template;
      templ.Format = format;
      templ.Culture = culture;
      templ.Engine = engine;
      templ.OwnerId = ownerId;
      return templ; 
    }
  }
}
