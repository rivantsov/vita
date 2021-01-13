using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Vita.Tools.DbFirst;

namespace Vita.Tools {
  public static class ToolsExtensions {

    public static string GetValue(this XmlNode xmlNode, string name, string defaultValue = null) {
      // if(!name.StartsWith("//"))
      // name = "//" + name;
      foreach(var ch in xmlNode.ChildNodes) {
        var el = ch as XmlElement;
        if(el != null && el.Name == name)
          return el.InnerText.Trim();
      }
      return defaultValue;
    }

    public static List<string> GetValueList(this XmlNode xmlNode, string path) {
      var options = xmlNode.GetValue(path).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
             .Select(o => o.Trim()).ToList();
      return options;
    }

    public static bool IsSet(this DbFirstOptions options, DbFirstOptions option) {
      return (options & option) != 0;
    }

  }
}
