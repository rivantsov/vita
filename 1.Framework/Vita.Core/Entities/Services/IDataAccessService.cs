using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data;


namespace Vita.Entities.Services {

  /// <summary>Provides an interface to the databases. The main purpose is to manage access to one or more databases undlerlying 
  /// the entity model. </summary>
  /// <remarks>Manages access to one or more physical databases with associated cache. Identical to IDataStore, but 
  /// allows access to one of several data stores.</remarks>
  public interface IDataAccessService : IDataStore {
  }


}
