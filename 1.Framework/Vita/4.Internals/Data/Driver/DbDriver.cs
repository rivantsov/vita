using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Data.Model;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Logging;
using Vita.Data.Driver.InfoSchema;
using System.Threading.Tasks;
using System.Data.Common;
using System.Threading;
using Vita.Data.Sql;
using Vita.Data.Runtime;
using Vita.Data.Linq;
using Vita.Data.Driver.TypeSystem;

namespace Vita.Data.Driver {


  public abstract class DbDriver {
    public readonly DbServerType ServerType;
    public readonly DbFeatures Features;

    public abstract DbOptions GetDefaultOptions();

    // Must be created in derived constructor
    public DbSqlDialect SqlDialect { get; protected set; }
    public int MaxKeyNameLength = 40;
    public IDbTypeRegistry TypeRegistry; 

    public DbDriver(DbServerType serverType, DbFeatures features) {
      ServerType = serverType;
      Features = features;
    }


    #region virtual and abstract methods - to be overridden to customize behavior for particular server
    public abstract DbModelLoader CreateDbModelLoader(DbSettings settings, ILog log);
    public abstract DbModelUpdater CreateDbModelUpdater(DbSettings settings);

    public virtual DbCrudSqlBuilder CreateCrudSqlBuilder(DbModel dbModel) {
      return new DbCrudSqlBuilder(dbModel); 
    }

    public virtual DbLinqSqlBuilder CreateLinqSqlBuilder(DbModel dbModel, LinqCommand command) {
      return new DbLinqSqlBuilder(dbModel, command); 
    }

    public virtual DbLinqNonQuerySqlBuilder CreateLinqNonQuerySqlBuilder(DbModel dbModel, NonQueryLinqCommand command) {
      return new DbLinqNonQuerySqlBuilder(dbModel, command); 
    }

    public abstract IDbConnection CreateConnection(string connectionString);
    public abstract IDbCommand CreateCommand();

    #endregion

    public bool Supports(DbFeatures feature) {
      return (Features & feature) != 0;
    }

    public virtual bool IsSystemSchema(string schema) {
      return false; 
    }

    public virtual string GetServerVersion(string connectionString) {
      var conn =  CreateConnection(connectionString) as System.Data.Common.DbConnection;
      if (conn == null) 
        return string.Empty; 
      return conn.ServerVersion;
    }

    public virtual object ExecuteCommand(IDbCommand command, DbExecutionType executionType) {
      try {
        var conn = command.Connection;
        if(conn.State != ConnectionState.Open)
          conn.Open();
        switch (executionType) {
          case DbExecutionType.Reader:
            return command.ExecuteReader();
          case DbExecutionType.NonQuery:
            return command.ExecuteNonQuery();
          case DbExecutionType.Scalar:
            return command.ExecuteScalar();
        }
        return null; //never happens
      } catch (System.Data.Common.DbException dbExc) {
        // Important: in some cases exception on invalid SQL is not thrown immediately but is thrown later when we try to read the results
        // ex (MS SQL): WHERE "Name" LIKE 'ABC%' ESCAPE '' - with empty ESCAPE arg string.
        // Debug.WriteLine("Failed SQL: \r\n" + command.CommandText);
        var dex = ConvertToDataAccessException(dbExc, command);
        throw dex;
      } 
    }

    public virtual Task<DbDataReader> ExecuteReaderAsync(DbCommand command, CancellationToken cancellationToken) {
      try {
        var conn = command.Connection;
        if (conn.State != ConnectionState.Open)
          conn.Open();
        return command.ExecuteReaderAsync(cancellationToken);
      } catch (System.Data.Common.DbException dbExc) {
        var dex = ConvertToDataAccessException(dbExc, command);
        throw dex;
      }
    }

    public virtual DataAccessException ConvertToDataAccessException(Exception exception, IDbCommand command) {
      var dex = new DataAccessException(exception, command);
      dex.AddValue(DataAccessException.KeyDbCommand, command);
      ClassifyDatabaseException(dex, command); //throws if different
     // Debug.WriteLine("DbCommand: " + command.ToLogString());
      return dex;
    }

    public virtual void CommandExecuted(DataConnection connection, IDbCommand command, DbExecutionType executionType) {
    }

    // The method should check specific exception types.
    public virtual void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
    }

    public virtual void OnDbModelConstructing(DbModel dbModel) {
    }
    
    public virtual void OnDbModelConstructed(DbModel dbModel) {
    }

    public virtual System.Data.IsolationLevel GetIsolationLevel(LockType lockType) {
      return System.Data.IsolationLevel.Unspecified; 
    }

    public virtual InfoTable ExecuteRawSelect(string connectionString, string sql) {
      IDbConnection connection = null;
      IDbCommand cmd = null;
      try {
        connection = CreateConnection(connectionString);
        connection.Open();
        cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var reader = (IDataReader)ExecuteCommand(cmd, DbExecutionType.Reader);
        var table = new InfoTable(reader);
        table.Load(reader);
        reader.Dispose(); //should be Dispose, not Close for .NET core
        return table;
      } finally {
        if(cmd != null)
          cmd.Connection = null;
        if(connection != null)
          connection.Close();
      }
    }


  }//class

}//namespace
