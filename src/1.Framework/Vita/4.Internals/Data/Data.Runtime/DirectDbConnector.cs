using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using Vita.Entities;
using Vita.Entities.Runtime;

namespace Vita.Data.Runtime {

  class DirectDbConnector : IDirectDbConnector {
    EntitySession _session;
    Database _database;
    DataConnection _connection;

    public IDbConnection DbConnection => _connection.DbConnection;
    public IDbTransaction DbTransaction => _connection.DbTransaction;

    public DirectDbConnector(EntitySession session, bool admin) {
      Util.CheckParam(session, nameof(session));
      _session = session;
      var dsServ = session.Context.App.GetService<IDataAccessService>();
      var ds = dsServ.GetDataSource(session.Context);
      Util.Check(ds != null, "Datasource {0} not found.", session.Context.DataSourceName);
      _database = ds.Database;
      _connection = _database.GetConnection(session, DbConnectionLifetime.Explicit, admin: admin);
    }

    public void Abort() {
      _connection.Abort();
    }

    public void BeginTransaction(bool commitOnSave = true, IsolationLevel isolationLevel = IsolationLevel.Unspecified) {
      _connection.BeginTransaction(commitOnSave, isolationLevel);
    }

    public void OpenConnection() {
      _connection.Open();
    }
    public void CloseConnection() {
      _connection.Close();
      _session.CurrentConnection = null; 
    }
    public void Commit() {
      _connection.Commit(); 

    }

    public void Dispose() {
      _connection.Dispose(); 
    }

    public int ExecuteNonQuery(IDbCommand command) {
      return (int)_database.ExecuteDirectDbCommand(command, _connection, DbExecutionType.NonQuery);
    }

    public IDataReader ExecuteSelect(IDbCommand command) {
      return (IDataReader) _database.ExecuteDirectDbCommand(command, _connection, DbExecutionType.Reader);
    }

    public T ExecuteScalar<T>(IDbCommand command) {
      var result = _database.ExecuteDirectDbCommand(command, _connection, DbExecutionType.Scalar);
      if(result == null)
        return default(T);
      return (T)result; 
    }

  } //class
}
