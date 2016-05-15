using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Vita.Data {


  /// <summary>Management of data sources inside data access service. </summary>
  public interface IDataSourceManagementService {
    IEnumerable<DataSource> GetDataSources();
    DataSource GetDataSource(string name = DataSource.DefaultName);
    void RegisterDataSource(DataSource dataSource);
    DataSourceEvents Events { get; }
  }

}
