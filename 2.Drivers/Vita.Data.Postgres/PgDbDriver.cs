using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Npgsql;
using NpgsqlTypes;

using Vita.Entities;
using Vita.Common;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Logging;

namespace Vita.Data.Postgres {

  public class PgDbDriver : DbDriver {
    public const DbOptions DefaultPgDbOptions = DbOptions.UseRefIntegrity | DbOptions.AutoIndexForeignKeys | DbOptions.UseStoredProcs; 
    public const DbFeatures PgFeatures =
      DbFeatures.Schemas | DbFeatures.StoredProcedures | DbFeatures.OutputParameters | DbFeatures.DefaultParameterValues
        | DbFeatures.ReferentialConstraints | DbFeatures.ClusteredIndexes 
        | DbFeatures.Views | DbFeatures.MaterializedViews | DbFeatures.Sequences | DbFeatures.ArrayParameters
        | DbFeatures.SkipTakeRequireOrderBy | DbFeatures.AllowsFakeOrderBy
        | DbFeatures.Paging | DbFeatures.BatchedUpdates | DbFeatures.NoIndexOnForeignKeys
        | DbFeatures.OrderedColumnsInIndexes | DbFeatures.UpdateFromSubQuery;


    public PgDbDriver()  : base(DbServerType.Postgres, PgFeatures) {
      base.DynamicSqlParameterPrefix = "@"; // "@";
      //PG does not require any special prefix for function parameters. But to avoid name collision with reserved words, we add this prefix
      base.StoredProcParameterPrefix = "p_"; 
      //Not sure SELECT is appropriate here, but nothing else works (including PERFORM)
      base.CommandCallFormat = "SELECT {0} ({1});";
      base.CommandCallOutParamFormat = "{0}";
      base.BatchBeginTransaction = "START TRANSACTION;";
      base.BatchCommitTransaction = "COMMIT;";
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


    protected override DbTypeRegistry CreateTypeRegistry() {
      return new PgTypeRegistry(this); 
    }

    public override IDbConnection CreateConnection(string connectionString) {
      var conn = new NpgsqlConnection(connectionString);
      return conn; 
    }

    public override DbSqlBuilder CreateDbSqlBuilder(DbModel dbModel) {
      return new PgDbSqlBuilder(dbModel);
    }
    public override DbModelUpdater CreateDbModelUpdater(DbSettings settings) {
      return new PgDbModelUpdater(settings);
    }
    public override DbModelLoader CreateDbModelLoader(DbSettings settings, MemoryLog log) {
      return new PgDbModelLoader(settings, log);
    }

    public override LinqSqlProvider CreateLinqSqlProvider(DbModel dbModel) {
      return new PgLinqSqlProvider(dbModel);
    }

    public override string GetFullName(string schema, string name) {
      return schema + ".\"" +  name + "\"";
    }


    public override void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
      var npExc = dataException.InnerException as NpgsqlException;
      if (npExc == null) //should never happen
        return;
      //npExc.ErrorCode is a strange large number; we use Code (string) instead, converting it to int
      int iCode;
      if (int.TryParse(npExc.Code, out iCode))
        dataException.ProviderErrorNumber = iCode;  
      switch (iCode) {
        case 23505: //unique index violation
          dataException.SubType = DataAccessException.SubTypeUniqueIndexViolation;
          var indexName = ExtractIndexName(npExc.Message);
          dataException.Data[DataAccessException.KeyDbKeyName] = indexName;
          break;
        case 23503: //integrity violation
          dataException.SubType = DataAccessException.SubTypeIntegrityViolation;
          break; 

      }
    }

    /* For Npgsql version 3.0+
         public override void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
          var npExc = dataException.InnerException as PostgresException
          if (npExc == null) //should never happen
            return;
          //npExc.ErrorCode is a strange large number; we use Code (string) instead, converting it to int
          int iCode;
          if (int.TryParse(npExc.SqlState, out iCode))
            dataException.ProviderErrorNumber = iCode;  
          switch (npExc.SqlState) {
            case "23505": //unique index violation
              dataException.SubType = DataAccessException.SubTypeUniqueIndexViolation;
              var indexName = ExtractIndexName(npExc.Message);
              dataException.Data[DataAccessException.KeyDbKeyName] = indexName;
              break;
            case "23503": //integrity violation
              dataException.SubType = DataAccessException.SubTypeIntegrityViolation;
              break; 
          }
        }
           */

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


    public override object ExecuteCommand(IDbCommand command, DbExecutionType executionType) {
      // this is one strange thing about PostGres - 'select-like' stored proc returns cursor, 
      // which is immediately disposed after trans is committed. 
      // As any standalone SQL command is auto-wrapped into transation, then simply calling stored proc (function) returns a deallocated cursor. 
      // You have to wrap the call in explicit transation, finish reading the data and then commit transaction
      // Here we only begin transaction, it will be committed after reading the data
      if(command.CommandType == CommandType.StoredProcedure && executionType == DbExecutionType.Reader && command.Transaction == null) {
        var conn = command.Connection;
        if(conn.State != ConnectionState.Open)
          conn.Open();
        command.Transaction = conn.BeginTransaction();
      }
      return base.ExecuteCommand(command, executionType);
    }

    public override void CommandExecuted(DataConnection connection, IDbCommand command, DbExecutionType executionType) {
      //If there is transaction started only for this command (see ExecuteCommand method above), then commit it
      if(command.CommandType == CommandType.StoredProcedure && executionType == DbExecutionType.Reader && 
        command.Transaction != null && command.Transaction != connection.DbTransaction) {
        command.Transaction.Commit();
        command.Transaction = null;
      }      
    }

    public override IDbDataParameter AddParameter(IDbCommand command, DbParamInfo prmInfo) {
      var prm = (NpgsqlParameter) base.AddParameter(command, prmInfo);
      prm.NpgsqlDbType = (NpgsqlDbType) prmInfo.TypeInfo.VendorDbType.VendorDbType;
      return prm; 
    }    
  }//class
}
