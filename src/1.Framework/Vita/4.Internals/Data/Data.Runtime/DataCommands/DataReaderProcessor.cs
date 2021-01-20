using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation;
using Vita.Data.Runtime;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Runtime;

namespace Vita.Data.Runtime {

  public class DataReaderProcessor : IDataCommandResultProcessor {
    public Func<IList> RowListCreator; //Creates empty generic List<T> list to fill up the results
    public RowListProcessor RowListProcessor;
    public Func<IDataRecord, EntitySession, object> RowReader;

    public object ProcessResult(DataCommand command) {
      var session = command.Connection.Session;
      session.CurrentQueryResultsWeakSet = new QueryResultsWeakSet(); // for SmartLoad
      var reader = command.Result as IDataReader;
      var resultList = RowListCreator(); 
      while(reader.Read()) {
        var row = this.RowReader(reader, session);
        //row might be null if authorization filtered it out or if it is empty value set from outer join
        if(row != null)
          resultList.Add(row);
      }
      reader.Close();
      command.RowCount = resultList.Count;
      session.CurrentQueryResultsWeakSet = null; 
      if(RowListProcessor == null)
        return resultList; 
      else 
        return RowListProcessor.ProcessRows(resultList);
    }

  }
}
