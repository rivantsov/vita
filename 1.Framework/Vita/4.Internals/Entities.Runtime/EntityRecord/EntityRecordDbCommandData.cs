using System;
using System.Collections.Generic;
using System.Data;

using Vita.Data.Model;

namespace Vita.Entities.Runtime {

  /// <summary>Per-record data containing information on output parameters of the db command during save operation. </summary>
  // Note: In case you think it can be done simpler - keep in mind that in batch mode one db command saves multiple records, 
  // so simply iterating thru cmd.Parameters and using DbParameter.SourceColumn property to find column/member to update - 
  // this does NOT work! You need explicit association of a record with db command output parameters
  public class EntityRecordDBCommandData {
    public IDbCommand DbCommand;
    public IList<OutParamInfo> OutputParameters = new List<OutParamInfo>();
  }

  public class OutParamInfo {
    public IDataParameter Parameter;
    public DbColumnInfo Column; 
  }
}
