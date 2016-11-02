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

  /// <summary>A helper container class for a single value returned by Web API. </summary>
  /// <remarks>There is a reported bug in Angular: ngresource reads a string as an array of characters. 
  /// (https://github.com/angular/angular.js/issues/2664). To help clients with this, the API methods that need to return a single string 
  /// return it as boxed inside this simple object.</remarks>
  /// <typeparam name="T">Value type.</typeparam>
  public class BoxedValue<T> {
    public readonly T Value; 
    public BoxedValue(T value) {
      Value = value; 
    }

  }

}
