using System;
using System.Data;
using System.Data.SqlClient;
using Microsoft.SqlServer.Server;
using System.Diagnostics;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Driver;
using System.Collections.Generic;
using Vita.Entities.Locking;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;
using System.Collections;
using Vita.Data.SqlGen;
using Vita.Data.Linq;

namespace Vita.Data.MsSql {

  public class MsSqlDbDriver : DbDriver {
    public const DbOptions DefaultMsSqlDbOptions = DbOptions.UseRefIntegrity | DbOptions.ShareDbModel 
                                                 | DbOptions.AutoIndexForeignKeys | DbOptions.UseBatchMode;
    public const DbFeatures MsSql12Features =
      DbFeatures.Schemas | DbFeatures.StoredProcedures | DbFeatures.OutputParameters | DbFeatures.DefaultParameterValues
        | DbFeatures.ReferentialConstraints | DbFeatures.ClusteredIndexes | DbFeatures.DefaultCaseInsensitive
        | DbFeatures.Views | DbFeatures.MaterializedViews | DbFeatures.ArrayParameters
        | DbFeatures.OrderedColumnsInIndexes | DbFeatures.IncludeColumnsInIndexes | DbFeatures.FilterInIndexes
        | DbFeatures.SkipTakeRequireOrderBy | DbFeatures.AllowsFakeOrderBy
        | DbFeatures.BatchedUpdates | DbFeatures.OutParamsInBatchedUpdates
        | DbFeatures.Paging | DbFeatures.TreatBitAsInt | DbFeatures.UpdateFromSubQuery
        | DbFeatures.HeapTables | DbFeatures.Sequences | DbFeatures.ServerPreservesComments;

    public string SystemSchema = "dbo"; //schema used for creating Vita_ArrayAsTable table type

    public override DbOptions GetDefaultOptions() {
      return DefaultMsSqlDbOptions;
    }

    public MsSqlDbDriver() : base(DbServerType.MsSql, MsSql12Features) {
      base.TypeRegistry = new MsSqlTypeRegistry(this);
      base.SqlDialect = new MsSqlDialect(this); 
    }


    public override bool IsSystemSchema(string schema) {
      if(string.IsNullOrWhiteSpace(schema))
        return false;
      schema = schema.ToLowerInvariant();
      switch(schema) {
        case "sys": case "information_schema":  case "guest":
          return true;
        default:
          return false;
      }
    }

    public override IDbConnection CreateConnection(string connectionString) {
      return new SqlConnection(connectionString);
    }

    public override DbModelLoader CreateDbModelLoader(DbSettings settings, IActivationLog log) {
      return new MsSqlDbModelLoader(settings, log);
    }
    public override DbModelUpdater CreateDbModelUpdater(DbSettings settings) {
      return new MsSqlDbModelUpdater(settings); 
    }
    public override DbSqlBuilder CreateDbSqlBuilder(DbModel dbModel, QueryInfo queryInfo) {
      return new MsSqlBuilder(dbModel, queryInfo); 
    }
    public override IDbCommand CreateCommand() {
      return new SqlCommand();
    }


    public override IDbDataParameter AddParameter(IDbCommand command, string name, DbStorageType typeDef, ParameterDirection direction, object value) {
      var prm = (SqlParameter) base.AddParameter(command, name, typeDef, direction, value);
      prm.SqlDbType = (SqlDbType) typeDef.CustomDbType;
     // prm.Size = (int) typeDef.Size;
      if(prm.SqlDbType == SqlDbType.Structured) {
        prm.TypeName = typeDef.TypeName;//.SqlTypeSpec; //Need TypeName for table-type params
        // For table-typed parameters, if the parameter contains empty table, it SHOULD be sent as 'null' (not DbNull)
        // Otherwise server throws exc; so 'null' should NOT be changed to DbNull
        prm.Value = value; 
      } else 
        prm.Value = value == null ? DBNull.Value : value;
      return prm;
    }

    public override void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
      var sqlEx = dataException.InnerException as SqlException;
      if (sqlEx == null) return;
      dataException.ProviderErrorNumber = sqlEx.Number;
      switch (sqlEx.Number) {
        case 2601: //unique index violation
          //sqlEx.Data contains 6 values which are mostly useless (info about provider)
          // we need to parse index name - stupidly the ex does not provide it as a separate value. 
          dataException.SubType = DataAccessException.SubTypeUniqueIndexViolation;
          dataException.Data[DataAccessException.KeyDbKeyName] = ExtractIndexNameFromDuplicateErrorMessage(sqlEx.Message); 
          break; 
        case 1205: //Transaction deadlock lock, process killed
          dataException.SubType = DataAccessException.SubTypeDeadLock;
          break; 
        case 547: // FK constraint violation on delete
          dataException.SubType = DataAccessException.SubTypeIntegrityViolation;
          break;
        case 50000:
          // We raise error with custom message, this results in error# 50000
          // RAISEERROR:  When msg_str is specified, RAISERROR raises an error message with an error number of 50000
          // we put tag into error message
          dataException.SubType = DataAccessException.SubTypeConcurrentUpdate;
          var segms = dataException.Message.Split('/');
          if (segms.Length > 1)
            dataException.Data[DataAccessException.KeyEntityName] = segms[1];
          break; 
        default: 
          if (sqlEx.Number >= 50000) {
            //this number is for messages that are NOT registered in SQL server Messages table
          }
          break; 
      }//switch
    }

    public override void OnDbModelConstructing(DbModel dbModel) {
      base.OnDbModelConstructing(dbModel);
      dbModel.AddCustomType(new DbCustomTypeInfo(dbModel, SystemSchema, MsSqlTypeRegistry.ArrayAsTableTypeName, DbCustomTypeKind.ArrayAsTable));
    }

    private string ExtractIndexNameFromDuplicateErrorMessage(string message) {
      try {
        // Sample error message (it is two-line string): 
        // Cannot insert duplicate key row in object 'books.Publisher' with unique index 'IXU_Publisher_Name'.
        // The statement has been terminated.
        var uniqueIndexTag = "unique index";
        var uiPos = message.IndexOf(uniqueIndexTag);
        var q1Pos = message.IndexOf("'", uiPos);
        var q2Pos = message.IndexOf("'", q1Pos + 1);
        var indexName = message.Substring(q1Pos + 1, q2Pos - q1Pos - 1);
        return indexName;
      } catch (Exception ex) {
        return "(failed to identify index: " + ex.Message + ")"; //
      }
    }

    //MS SQL requires specific isolation mode for transactional load/update to work correctly without deadlocks
    public override IsolationLevel GetIsolationLevel(LockType lockType) {
      switch(lockType) {
        case LockType.ForUpdate: return IsolationLevel.ReadCommitted;
        case LockType.SharedRead: return IsolationLevel.Snapshot;
        default: return IsolationLevel.Unspecified; 
      }
    }

  }//class
}
