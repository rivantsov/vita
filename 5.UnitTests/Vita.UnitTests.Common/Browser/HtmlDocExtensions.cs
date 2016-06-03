using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vita.UnitTests.Common {

  public static class HtmlDocExtensions {

    public static HtmlElement FindButton(this HtmlDocument doc, string name) {
      return doc.FindElementByName("button", name);
    }//method

    public static HtmlElement FindInput(this HtmlDocument doc, string name) {
      return doc.FindElementByName("input", name);
    }//method

    public static HtmlElement FindElementByName(this HtmlDocument doc, string tagName, string name) {
      var elems = doc.GetElementsByTagName(tagName);
      foreach(HtmlElement el in elems)
        if(el.Name == name)
          return el;
      return null;
    }//method

    public static IList<HtmlElement> FindElements(this HtmlDocument doc, string tagName, Func<HtmlElement, bool> func) {
      var result = new List<HtmlElement>();
      var elems = doc.GetElementsByTagName(tagName);
      foreach(HtmlElement el in elems)
        if(func(el))
          result.Add(el);
      return result;
    }
    public static HtmlElement FindElement(this HtmlDocument doc, string tagName, Func<HtmlElement, bool> func) {
      var elems = doc.GetElementsByTagName(tagName);
      foreach(HtmlElement el in elems)
        if(func(el))
          return el; 
      return null;
    }

    public static bool IsVisible(this HtmlElement elem) {
      return elem.OffsetParent != null;
    }
  }
}

