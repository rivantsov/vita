using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Utilities {
  /// <summary>A class representing template with named args instead of indexes in place holders, ex: "Some text {arg1}, {arg2}"</summary>
  public class StringTemplate {
    public readonly string OriginalTemplate;
    public readonly string StandardForm;
    public readonly string[] ArgNames; 

    private StringTemplate(string template, string standardForm, string[] argNames) {
      OriginalTemplate = template;
      StandardForm = standardForm;
      ArgNames = argNames; 
    }

    public static StringTemplate Empty = new StringTemplate(string.Empty, string.Empty, new string[0]);

    /// <summary>Parses template with named arguments and converts it to standard form with list of arg names attached.
    /// Converts all brace-enclosed names ({value}) into placeholders like {0}. </summary>
    /// <param name="template">Template to convert.</param>
    /// <returns>StringTemplate object.</returns>
    /// <remarks>Does not support escaped handles like {{ or }} </remarks>
    public static StringTemplate Parse(string template) {
      if(string.IsNullOrEmpty(template))
        return StringTemplate.Empty;
      var names = new List<string>();
      var segms = new List<string>(); 
      var currPos = 0;
      while(currPos < template.Length) {
        // find {
        var openBr = template.IndexOf('{', currPos);
        if(openBr < 0)
          openBr = template.Length; //assume it is after last char
        // add segment before '{'
        if(openBr > currPos) {
          var segm = template.Substring(currPos, openBr - currPos);
          segms.Add(segm);
          if(openBr > template.Length - 1)
            break; //we are done
        }
        //find close brace '{'
        var closeBr = template.IndexOf('}', openBr + 1);
        Util.Check(closeBr > 0, "Malformed template, no closing brace for open brace at {0}; template: {1}", currPos, template);
        var name = template.Substring(openBr + 1, closeBr - openBr - 1);
        Util.CheckNotEmpty(name, "Invalid template, empty arg: {0}", template);
        // check if name already used
        var index = names.IndexOf(name);
        if (index < 0) {
          index = names.Count;
          names.Add(name); 
        }
        // add segment with index instead of name
        segms.Add("{" + index + "}");
        //move curr pos beyond close brace
        currPos = closeBr + 1;
      }//while
      var standardForm = string.Join(string.Empty, segms);
      return new StringTemplate(template, standardForm, names.ToArray());
    }


  }//class
}
