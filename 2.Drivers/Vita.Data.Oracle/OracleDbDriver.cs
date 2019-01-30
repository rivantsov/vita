using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Vita.Data.Driver;
using Vita.Data.Driver.InfoSchema;
using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Data.Runtime;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Logging;

using Oracle.ManagedDataAccess.Client;

namespace Vita.Data.Oracle {
  public class OracleDbDriver : DbDriver {
    public const DbFeatures OracleFeatures = 
        DbFeatures.Schemas |  DbFeatures.OutputParameters | DbFeatures.DefaultParameterValues
        | DbFeatures.ReferentialConstraints
        // | DbFeatures.ClusteredIndexes -- PK is always clustered index
        | DbFeatures.Views
        //-- initial release - no support for mat views, it gets messed up; Oracle actually creates a real table instead of mat view
        // | DbFeatures.MaterializedViews 

        //-- formally supported but with DESC index column turns into some 'function' under system name, so index info cannot be loaded 
        // and matched correctly
        // | DbFeatures.OrderedColumnsInIndexes 
        | DbFeatures.SkipTakeRequireOrderBy
        | DbFeatures.TreatBitAsInt
        | DbFeatures.BatchedUpdates | DbFeatures.OutParamsInBatchedUpdates
        | DbFeatures.Paging | DbFeatures.UpdateFromSubQuery
        | DbFeatures.Sequences;
    public DbOptions DefaultOracleDbOptions = DbOptions.UseRefIntegrity | DbOptions.ShareDbModel | DbOptions.AutoIndexForeignKeys;

    /// <summary>Settings key for default tablespace value in DbSettings.CustomSettings dictionary.</summary>
    public const string SettingsKeyDefaultTableSpace = "KeyDefaultTableSpace";
    /// <summary>Settings key for default temp tablespace value in DbSettings.CustomSettings dictionary.</summary>
    public const string SettingsKeyDefaultTempTableSpace = "KeyDefaultTempTableSpace";

    public OracleDbDriver() : base(DbServerType.Oracle, OracleFeatures) {
      base.TypeRegistry = new OracleDbTypeRegistry(this); 
      base.SqlDialect = new OracleSqlDialect(this);
      base.MaxKeyNameLength = 30;
    }

    public override void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
      var oracleEx = dataException.InnerException as OracleException;
      if(oracleEx == null)
        return;
      dataException.ProviderErrorNumber = oracleEx.Number;
      switch(oracleEx.Number) {
        case 1: //unique index violation
          //sqlEx.Data contains 6 values which are mostly useless (info about provider)
          // we need to parse index name - stupidly the ex does not provide it as a separate value. 
          dataException.SubType = DataAccessException.SubTypeUniqueIndexViolation;
          dataException.Data[DataAccessException.KeyDbKeyName] = ExtractIndexNameFromDuplicateErrorMessage(oracleEx.Message);
          break;
        case 60: //Transaction deadlock lock, process killed
          dataException.SubType = DataAccessException.SubTypeDeadLock;
          break;
        case 2291: // FK constraint violation on delete
        case 2292:
          dataException.SubType = DataAccessException.SubTypeIntegrityViolation;
          break;
      }//switch
    }

    private string ExtractIndexNameFromDuplicateErrorMessage(string message) {
      try {
        // Sample error message:ORA-00001: unique constraint (SYSTEM.IXU_misc_Driver_LicenseNumber) violated
        var p1Pos = message.IndexOf("(");
        var p2Pos = message.IndexOf(")");
        var indexName = message.Substring(p1Pos + 1, p2Pos - p1Pos - 1);
        if (indexName.Contains(".")) {
          var names = indexName.Split('.');
          indexName = names[1];
        }
        return indexName;
      } catch(Exception ex) {
        return "(failed to identify index: " + ex.Message + ")"; //
      }
    }

    public override DataAccessException ConvertToDataAccessException(Exception exception, IDbCommand command) {
      return base.ConvertToDataAccessException(exception, command);
    }

    public override IDbCommand CreateCommand() {
      var cmd = new OracleCommand();
      cmd.BindByName = true; // Important! Bind params by name, not position; default is false
      return cmd; 
    }

    public override IDbConnection CreateConnection(string connectionString) {
      return new OracleConnection(connectionString); 
    }

    public override DbModelLoader CreateDbModelLoader(DbSettings settings, IActivationLog log) {
      return new OracleDbModelLoader(settings, log);
    }

    public override DbModelUpdater CreateDbModelUpdater(DbSettings settings) {
      return new OracleDbModelUpdater(settings);
    }

    public override DbLinqSqlBuilder CreateLinqSqlBuilder(DbModel dbModel, LinqCommandInfo queryInfo) {
      return new OracleSqlBuilder(dbModel, queryInfo); 
    }
    public override object ExecuteCommand(IDbCommand command, DbExecutionType executionType) {
      // Oracle does not allow ending semicolon in single statement. 
      // But... batches start with BEGIN; and end with "END;"
      if (!command.CommandText.StartsWith("BEGIN "))
        command.CommandText = command.CommandText.TrimEnd(' ', ';', '\r', '\n');
      return base.ExecuteCommand(command, executionType);
    }


    public override Task<DbDataReader> ExecuteReaderAsync(DbCommand command, CancellationToken cancellationToken) {
      return base.ExecuteReaderAsync(command, cancellationToken); 
    }

    public override DbOptions GetDefaultOptions() {
      return DefaultOracleDbOptions;
    }

    public override IsolationLevel GetIsolationLevel(LockType lockType) {
      switch(lockType) {
        case LockType.ForUpdate:
          return IsolationLevel.ReadCommitted;
        case LockType.SharedRead:
          return IsolationLevel.Serializable;
        default:
          return IsolationLevel.Unspecified;
      }
    }

    public override string GetServerVersion(string connectionString) {
      using (var conn = CreateConnection(connectionString)) {
        var cmd = CreateCommand();
        cmd.CommandText = @"SELECT * FROM PRODUCT_COMPONENT_VERSION WHERE PRODUCT LIKE 'Oracle Database%'";
        cmd.Connection = conn;
        var reader = cmd.ExecuteReader();
        if(!reader.Read())
          return null;
        var version = (string) reader["version"];
        return version; 
      }
    }

    public override bool IsSystemSchema(string schema) {
      return false;
    }

    public override void OnDbModelConstructed(DbModel dbModel) {
      base.OnDbModelConstructed(dbModel); 
    }

    public override void OnDbModelConstructing(DbModel dbModel) {
      base.OnDbModelConstructing(dbModel);
    }
  }
}
