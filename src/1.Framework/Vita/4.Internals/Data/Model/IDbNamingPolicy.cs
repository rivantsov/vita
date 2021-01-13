using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Data.Model {

  public interface IDbNamingPolicy {
    /// <summary>Called for tables, columns, keys, indexes. </summary>
    /// <param name="dbObject">The object being named. </param>
    /// <returns>Should return the same or modified name.</returns>
    void CheckName(object dbObject);
  }

}
