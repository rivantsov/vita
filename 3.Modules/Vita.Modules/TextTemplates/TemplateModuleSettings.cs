using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Modules.TextTemplates {

  public class TemplateModuleSettings {
    public IDictionary<string, ITemplateEngine> Engines = new Dictionary<string, ITemplateEngine>(StringComparer.InvariantCultureIgnoreCase);

    public TemplateModuleSettings(IList<ITemplateEngine> engines = null) {
      var simpleEng = new SimpleTemplateEngine();
      Engines[simpleEng.Name] = simpleEng; 
      if(engines != null)
        foreach(var e in engines)
          Engines[e.Name] = e; 
    }

    public ITemplateEngine GetEngine(string engineName, bool throwIfNotFount = true) {
      ITemplateEngine result;
      if(Engines.TryGetValue(engineName, out result))
        return result;
      if(throwIfNotFount)
        Util.Throw("Template engine {0} not registered in Template module.", engineName);
      return null; 
    }

  
  }
}
