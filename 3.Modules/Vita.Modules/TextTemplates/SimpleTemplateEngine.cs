using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Modules.TextTemplates {
  public class SimpleTemplateEngine : ITemplateEngine {
    public string OpenTag = "<#";
    public string EndTag = "#>";
    public string EscapedOpenTag = "_<_#_"; //will be replaced with "<#"

    public string Name {
      get { return "Simple"; }
    }

    public string Transform(string template, TemplateFormat format, IDictionary<string, object> data) {
      var result = template; 
      var entries = data.ToList(); 
      foreach(var kv in entries) {
        //Quick check without braces/tags
        if(!result.Contains(kv.Key))
          continue;
        var tag = OpenTag + kv.Key + EndTag;
        var strValue = kv.Value + string.Empty; //safe ToString()
        if(!string.IsNullOrWhiteSpace(strValue) && format == TemplateFormat.Html)
          strValue = StringHelper.EscapeForHtml(strValue);
        result = result.Replace(tag, strValue);
      }
      if(result.Contains(EscapedOpenTag))
        result = result.Replace(EscapedOpenTag, OpenTag);
      return result; 
    }
  }
}
