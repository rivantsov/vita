using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Web {

  public enum ApiNameMapping {
    Default,
    CamelCase,
    UnderscoreAllLower,
  }

  /// <summary>A helper container class for a single value returned by Web API. There is a reported bug in Angular: ngresource reads a string as an array of characters. 
  /// So instead of returning a string, it is more convenient to return BoxedValue&lt;string&gt;.</summary>
  /// <typeparam name="T">Value type.</typeparam>
  public class BoxedValue<T> {
    public readonly T Value; 
    public BoxedValue(T value) {
      Value = value; 
    }

  }

}
