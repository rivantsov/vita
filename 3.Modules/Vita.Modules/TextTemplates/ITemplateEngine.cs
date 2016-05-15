using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.TextTemplates {

  public interface ITemplateEngine {
    string Name { get; }
    string Transform(string template, TemplateFormat format, IDictionary<string, object> data);
  }
}
