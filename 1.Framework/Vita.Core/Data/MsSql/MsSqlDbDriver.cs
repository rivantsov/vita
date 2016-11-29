using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Logging;
using Vita.Entities.Linq;
using Microsoft.SqlServer.Server;
using System.Collections.Generic;

namespace Vita.Data.MsSql {

  public enum MsSqlVersion {
    V2008,
    V2012
  }

  public class MsSqlDbDriver : DbDriver {
    public const DbOptions DefaultMsSqlDbOptions = DbOptions.Default | DbOptions.AutoIndexForeignKeys | DbOptions.UseBatchMode;
    public const DbFeatures MsSql08Features =
      DbFeatures.Schemas | DbFeatures.StoredProcedures | DbFeatures.OutputParameters | DbFeatures.DefaultParameterValues
        | DbFeatures.ReferentialConstraints | DbFeatures.ClusteredIndexes | DbFeatures.DefaultCaseInsensitive
        | DbFeatures.Views | DbFeatures.MaterializedViews | DbFeatures.ArrayParameters
        | DbFeatures.OrderedColumnsInIndexes | DbFeatures.IncludeColumnsInIndexes | DbFeatures.FilterInIndexes
        | DbFeatures.NoIndexOnForeignKeys 
        | DbFeatures.SkipTakeRequireOrderBy | DbFeatures.AllowsFakeOrderBy
        | DbFeatures.BatchedUpdates | DbFeatures.OutParamsInBatchedUpdates
        | DbFeatures.Paging | DbFeatures.TreatBitAsInt | DbFeatures.UpdateFromSubQuery
        ;
    public const DbFeatures MsSql12Features = MsSql08Features | DbFeatures.HeapTables | DbFeatures.Sequences;

    public readonly MsSqlVersion ServerVersion;
    public string SystemSchema = "dbo"; //schema used for creating Vita_ArrayAsTable table type
    // User-defined table type, created by VITA to be used to send array-type parameters to SQLs and stored procedures
    public const string ArrayAsTableTypeName = "Vita_ArrayAsTable";

    /// <summary>Automatically detects server version using provided connection string and returns a driver object with appropriate version.</summary>
    /// <param name="connectionString">Connection string to use to detect server version.</param>
    /// <returns>An driver instance.</returns>
    public static MsSqlDbDriver Create(string connectionString) {
      var version = DetectServerVersion(connectionString);
      return new MsSqlDbDriver(version);
    }

    public MsSqlDbDriver() : this(MsSqlVersion.V2012) {}

    public MsSqlDbDriver(MsSqlVersion serverVersion)
                   : base(DbServerType.MsSql, serverVersion == MsSqlVersion.V2008 ? MsSql08Features : MsSql12Features) {
      ServerVersion = serverVersion;
      base.DynamicSqlParameterPrefix = base.StoredProcParameterPrefix = "@";
      base.CommandCallFormat = "EXEC {0} {1};";
      base.CommandCallOutParamFormat = "{0} OUTPUT";
      base.BatchBeginTransaction = "BEGIN TRANSACTION;";
      base.BatchCommitTransaction = "COMMIT TRANSACTION;";
      base.DDLSeparator = Environment.NewLine + "GO" + Environment.NewLine; 
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

    // overrides
    protected override DbTypeRegistry CreateTypeRegistry() {
      return new MsSqlTypeRegistry(this); 
    }

    public override IDbConnection CreateConnection(string connectionString) {
      return new SqlConnection(connectionString);
    }

    public override DbSqlBuilder CreateDbSqlBuilder(DbModel dbModel) {
      return new MsSqlDbSqlBuilder(dbModel, this.ServerVersion); 
    }

    public override DbModelUpdater CreateDbModelUpdater(DbSettings settings) {
      return new MsSqlDbModelUpdater(settings);
    }

    public override DbModelLoader CreateDbModelLoader(DbSettings settings, MemoryLog log) {
      return new MsSqlDbModelLoader(settings, log); 
    }

    public override Vita.Data.Driver.LinqSqlProvider CreateLinqSqlProvider(DbModel dbModel) {
      return new MsSqlLinqSqlProvider(dbModel, ServerVersion);
    }

    public override void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
      var sqlEx = dataException.InnerException as SqlException;
      if (sqlEx == null) return;
      dataException.ProviderErrorNumber = sqlEx.ErrorCode;
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
        case 50000: //this number is for messages that are NOT registered in SQL server Messages table
          //custom error raised by CRUD stored proc. Message contains 4 segments: 'Code/op/table/pk', for ex: 'VITA:RowVersionConflict/Update/Employee/123'
          var msg = sqlEx.Message;
          if (msg.StartsWith(MsSqlDbSqlBuilder.ErrorTagConcurrentUpdateConflict)) {
            var segments = msg.SplitNames('/');
            var op = segments[1];
            var tableName = segments[2];
            var pk = segments[3];
            dataException.SubType = DataAccessException.SubTypeConcurrentUpdate;
            dataException.Data[DataAccessException.KeyTableName] = tableName; 
            dataException.Data[DataAccessException.KeyRowPrimaryKey] = pk; 
          }//if
          break;
      }//switch
    }

    public override void OnDbModelConstructing(DbModel dbModel) {
      base.OnDbModelConstructing(dbModel);
      dbModel.AddCustomType(new DbCustomTypeInfo(dbModel, SystemSchema, ArrayAsTableTypeName, DbCustomTypeKind.ArrayAsTable));
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

    public override IDbDataParameter AddParameter(IDbCommand command, DbParamInfo prmInfo)  {
      var prm = (SqlParameter)base.AddParameter(command, prmInfo);
      prm.SqlDbType = (SqlDbType)prmInfo.TypeInfo.VendorDbType.VendorDbType;
      //Set User type name
      switch(prm.SqlDbType) {
        case SqlDbType.Udt:
          prm.UdtTypeName = prmInfo.TypeInfo.SqlTypeSpec;
          break; 
        case SqlDbType.Structured:
          prm.TypeName = prmInfo.TypeInfo.SqlTypeSpec;
          break; 
      }
      return prm;
    }

    //MS SQL requires specific isolation mode for transactional load/update to work correctly without deadlocks
    public override IsolationLevel GetIsolationLevel(LinqCommandFlags flags) {
      if (flags.IsSet(LinqCommandFlags.WriteLock))
        return IsolationLevel.ReadCommitted;
      else if (flags.IsSet(LinqCommandFlags.ReadLock))
        return IsolationLevel.Snapshot;
      else
        return IsolationLevel.Unspecified;
      
    }

    // Utility method to detect version
    public static MsSqlVersion DetectServerVersion(string connectionString) {
      using (var conn = new SqlConnection(connectionString)) {
        conn.Open();
        var serverVersion = conn.ServerVersion;
        conn.Close();
        if (serverVersion.StartsWith("11."))
          return MsSqlVersion.V2012;
        if (serverVersion.StartsWith("12."))
          return MsSqlVersion.V2012; //2014 is supported but treated as 2012
        //otherwise return 2008
        return MsSqlVersion.V2008;
      }//using
    }

    // Used for sending lists in SqlParameter, to use in SQL clauses like 'WHERE x IN (@P0)"
    // @P0 should be declarate as VITA_ArrayAsTable data type. 
    // We can use DataTable as a container, but DataTable is not supported by .NET core;
    // we use alternative: IEnumerable<SqlDataRecord>, it is supported. 
    // TODO: review the method and optimize it. 
    internal static object ConvertListToRecordList(object value) {
      var list = value as System.Collections.IEnumerable;
      var valueType = value.GetType();
      Type elemType;
      Util.Check(valueType.IsListOfDbPrimitive(out elemType),
        "Value must be list of DB primitives. Value type: {0} ", valueType);
      if(elemType.IsNullableValueType())
        elemType = Nullable.GetUnderlyingType(elemType); 
      bool isEnum = elemType.IsEnum;
      var records = new List<SqlDataRecord>();
      var colData = new SqlMetaData("Value", SqlDbType.Variant);
      foreach(object v in list) {
        var rec = new SqlDataRecord(colData);
        var v1 = isEnum ? (int)v : v;
        rec.SetValue(0, v1);
        records.Add(rec);
      }
      if(records.Count == 0)
        return null; // with 0 rows throws error, advising to send NULL
      return records;
    }



  }//class
}
