using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Model;
using Vita.Entities.Logging;
using Vita.Entities.Services;
using Vita.Entities.Linq;

namespace Vita.Data.Driver {


  public abstract class DbDriver {
    public readonly DbServerType ServerType;
    public readonly DbFeatures Features;
    public int MaxParamCount = 2000;


    public DbTypeRegistry TypeRegistry {
      get {
        if(_typeRegistry == null)
          _typeRegistry = CreateTypeRegistry();
        return _typeRegistry;
      }
    } DbTypeRegistry _typeRegistry; 

    // Parameter prefix. For MS SQL, it is '@' for both. For MySql, we need to use '@' for dynamic SQLs but no prefix or smth like 'prm' for stored procs
    // DbSqlBuilder chooses prefix depending on UseStoredProc flag in Settings.Options - which controls if app uses stored procs or dynamic SQL for CRUD 
    public string DynamicSqlParameterPrefix = string.Empty;
    public string StoredProcParameterPrefix = string.Empty;
    public const string GeneratedCrudProcTagPrefix = "-- VITA/Generated: ";
    public string CommandCallFormat = "CALL {0} ({1});";
    public string CommandCallOutParamFormat = "{0}";
    public string BatchBeginTransaction = "BEGIN TRANSACTION;";
    public string BatchCommitTransaction = "COMMIT TRANSACTION;";
    public string DDLSeparator = Environment.NewLine; //For MS SQL it is "GO"
    public char DefaultLikeEscapeChar = '\\'; // works for all except MySql - there we change it to '/';


    public DbDriver(DbServerType serverType, DbFeatures features) {
      ServerType = serverType;
      Features = features; 
    }


    #region virtual and abstract methods - to be overridden to customize behavior for particular server
    protected abstract DbTypeRegistry CreateTypeRegistry();
    public abstract DbModelLoader CreateDbModelLoader(DbSettings settings, SystemLog log);
    public abstract DbModelUpdater CreateDbModelUpdater(DbSettings settings);
    public abstract DbSqlBuilder CreateDbSqlBuilder(DbModel dbModel);
    public abstract LinqSqlProvider CreateLinqSqlProvider(DbModel dbModel);
    public abstract IDbConnection CreateConnection(string connectionString);
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

    public virtual string GetFullName(string schema, string name) {
      if(Supports(DbFeatures.Schemas))
        return string.Format("\"{0}\".\"{1}\"", schema, name);
      else
        return "\"" + name + "\"";
    }

    // Returns select-all SQL statement for a given table. Used in low-level, early access in system modules like DbInfo
    public virtual string GetDirectSelectAllSql(string schema, string tableName) {
      var sql = string.Format("SELECT * FROM {0};", GetFullName(schema, tableName));
      return sql; 
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

    public virtual IDbDataParameter AddParameter(IDbCommand command, DbParamInfo prmInfo) {
      var prm = command.CreateParameter();
      var typeInfo = prmInfo.TypeInfo;
      prm.DbType = typeInfo.DbType;
      prm.Direction = prmInfo.Direction;
      prm.ParameterName = prmInfo.Name;
      if (typeInfo.Precision > 0) prm.Precision = typeInfo.Precision;
      if (typeInfo.Scale > 0) prm.Scale = typeInfo.Scale;
      if (typeInfo.Size > 0) prm.Size = (int) typeInfo.Size;
      prm.Value = prmInfo.DefaultValue;
      command.Parameters.Add(prm); 
      return prm; 
    }

    // The method should check specific exception types.
    public virtual void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
    }

    public virtual void OnDbModelConstructing(DbModel dbModel) {
    }
    
    public virtual void OnDbModelConstructed(DbModel dbModel) {
    }

    public virtual System.Data.IsolationLevel GetIsolationLevel(LinqCommandFlags flags) {
      return System.Data.IsolationLevel.Unspecified; 

    }

  }//class

}//namespace
