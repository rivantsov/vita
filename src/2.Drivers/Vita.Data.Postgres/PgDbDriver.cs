using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Npgsql;
using NpgsqlTypes;

using Vita.Entities;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;
using System.Collections;
using Vita.Data.Linq;
using System.Threading;

namespace Vita.Data.Postgres {

  public class PgDbDriver : DbDriver {
    /// <summary>
    /// Leave it at TRUE!! new behavior is messed up, struggled with it for a while
    /// </summary>
    public static bool EnableLegacyTimestampBehavior = true;

    public const DbOptions DefaultPgDbOptions = DbOptions.UseRefIntegrity | DbOptions.AutoIndexForeignKeys; 
    public const DbFeatures PgFeatures =
      DbFeatures.Schemas | DbFeatures.StoredProcedures | DbFeatures.OutputParameters | DbFeatures.DefaultParameterValues
        | DbFeatures.ReferentialConstraints | DbFeatures.ClusteredIndexes | DbFeatures.InsertMany
        | DbFeatures.Views | DbFeatures.MaterializedViews | DbFeatures.Sequences | DbFeatures.ArrayParameters
        | DbFeatures.SkipTakeRequireOrderBy | DbFeatures.AllowsFakeOrderBy
        | DbFeatures.Paging | DbFeatures.BatchedUpdates
        | DbFeatures.OrderedColumnsInIndexes | DbFeatures.UpdateFromSubQuery
        | DbFeatures.ExceptOperator | DbFeatures.IntersectOperator;

    public PgDbDriver()  : base(DbServerType.Postgres, PgFeatures) {
      base.TypeRegistry = new PgTypeRegistry(this);
      base.SqlDialect = new PgSqlDialect(this);
      if (EnableLegacyTimestampBehavior) {
        // New behavior in npgsql 6; breaks backward compatibility; opting out
        //   https://www.npgsql.org/doc/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
      }
    }

    public override DbOptions GetDefaultOptions() {
      return DefaultPgDbOptions;
    }

    public override bool IsSystemSchema(string schema) {
      if(string.IsNullOrWhiteSpace(schema))
        return false;
      schema = schema.ToLowerInvariant();
      switch(schema) {
        case "public":
        case "information_schema": return true;
        default: return schema.StartsWith("pg_");
      }
    }

    public override IDbConnection CreateConnection(string connectionString) {
      var conn = new NpgsqlConnection(connectionString);
      return conn; 
    }

    public override DbModelUpdater CreateDbModelUpdater(DbSettings settings) {
      return new PgDbModelUpdater(settings);
    }
    public override DbModelLoader CreateDbModelLoader(DbSettings settings, ILog log) {
      return new PgDbModelLoader(settings, log);
    }

    public override DbLinqSqlBuilder CreateLinqSqlBuilder(DbModel dbModel, LinqCommand command) {
      return new PgLinqSqlBuilder(dbModel, command);
    }

    public override DbCrudSqlBuilder CreateCrudSqlBuilder(DbModel dbModel) {
      return new PgCrudSqlBuilder(dbModel);
    }

    public override IDbCommand CreateCommand() {
      return new NpgsqlCommand();
    }

    public override void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
      var pgExc = dataException.InnerException as PostgresException;// NpgsqlException;
      if (pgExc == null) //should never happen
        return;
      //npExc.ErrorCode is a strange large number; we use Code (string) instead, converting it to int
      switch (pgExc.SqlState) {
        case "23505": //unique index violation
          dataException.SubType = DataAccessException.SubTypeUniqueIndexViolation;
          var indexName = ExtractIndexName(pgExc.Message);
          dataException.Data[DataAccessException.KeyDbKeyName] = indexName;
          break;
        case "23503": //integrity violation
          dataException.SubType = DataAccessException.SubTypeIntegrityViolation;
          break; 
      }
    }

    private string ExtractIndexName(string message) {
      try {
        // Sample error message
        // ERROR: 23505: duplicate key value violates unique constraint "IXU_Publisher_Name"
        var q1Pos = message.IndexOf('"');
        var q2Pos = message.IndexOf('"', q1Pos + 1);
        var indexName = message.Substring(q1Pos + 1, q2Pos - q1Pos - 1);
        return indexName;
      } catch (Exception ex) {
        return "(failed to identify index: " + ex.Message + ")"; //
      }
    }

    // Probing async methods
    private async Task<object> ExecuteReaderAsync(CancellationToken token) {
      var sqlCmd = new NpgsqlCommand();
      var reader = await sqlCmd.ExecuteReaderAsync(token);
      while (await reader.ReadAsync()) {
        var someValue = reader["someValue"];
      }
      return new object();
    }

  }//class
}
