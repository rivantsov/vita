using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Vita.Data.Driver;
using Vita.Data.Linq.Translation;
using Vita.Data.Model;
using Vita.Data.SqlGen;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Runtime {

  public interface IDataCommandResultProcessor {
    object ProcessResult(DataCommand command);
  }

  public class DataCommand {
    public DataConnection Connection;
    public IDbCommand DbCommand;
    public DbExecutionType ExecutionType; 
    public IDataCommandResultProcessor ResultProcessor;
    public IList<EntityRecord> Records; // input records
    //batch commands only
    public IList<BatchParamCopy> ParamCopyList;

    // Results
    public object Result; // direct result, DataReader, not record list
    public object ProcessedResult; 
    public int RowCount = -1;
    public int TimeMs; 

    public DataCommand(DataConnection connection, IDbCommand dbCommand, DbExecutionType executionType, 
                                IDataCommandResultProcessor resultsProcessor, IList<EntityRecord> records,
                                IList<BatchParamCopy> paramCopyList = null) {
      Connection = connection;
      DbCommand = dbCommand;
      ExecutionType = executionType; 
      ResultProcessor = resultsProcessor;
      Records = records;
      ParamCopyList = paramCopyList; 
    }
  } //class

  [System.Diagnostics.DebuggerDisplay("{From.ParameterName}->{To.ParameterName}")]
  public class BatchParamCopy {
    public IDataParameter From;
    public IDataParameter To;
  }


}
