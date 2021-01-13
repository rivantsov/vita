using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Runtime;

namespace Vita.Data.Runtime {

  /// <summary>Management of data sources inside data access service. </summary>
  public interface IDataAccessService {
    IEnumerable<DataSource> GetDataSources();
    DataSource GetDataSource(OperationContext context);
    void RegisterDataSource(DataSource dataSource);
    void RemoveDataSource(string name); 
  }

}
