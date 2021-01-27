using System;
using System.Collections.Generic;
using System.Text;

namespace BookStore.GraphQLServer {
  public static class TypeListExtensions {
    
    // TODO: define this extension in NGraphQL
    public static void Add(this IList<Type> list, params Type[] types) {
      foreach (var type in types)
        list.Add(type); 
    }

  }
}
