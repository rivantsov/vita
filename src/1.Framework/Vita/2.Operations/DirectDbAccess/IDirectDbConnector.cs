using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities {


  /// <summary>Provides direct Db access. You can execute ADO.NET commands (SQL or stored procedures) using this facility.</summary>
  /// <remarks>Use session.GetDirectDbConnector extension method to obtain a connector. </remarks>
  public interface IDirectDbConnector : IDisposable {


    /*
    /// <summary>Logs the DB command details to the operation log.</summary>
    /// <param name="command">The command to execute.</param>
    void LogDbCommand(string message, IDbCommand command, int? rowCount = null, int? timeMs = null);
    */

    /// <summary>Opens the database connection associated with the facility and entity session. </summary>
    void OpenConnection();

    /// <summary>Closes the database connection associated with the facility and entity session. </summary>
    void CloseConnection();

    /// <summary>Starts the transaction on the connection associated with the facility and entity session.</summary>
    /// <param name="commitOnSave">True if transaction should be committed automatically after <c>session.SaveChanges</c> call; otherwise, false.</param>
    /// <param name="isolationLevel">Isolation level of the transaction.</param>
    void BeginTransaction(bool commitOnSave = true, IsolationLevel isolationLevel = IsolationLevel.Serializable);

    /// <summary>Commits the transaction on the connection associated with the facility and entity session.</summary>
    void Commit();

    /// <summary>Aborts the transaction on the connection associated with the facility and entity session.</summary>
    void Abort();

    /// <summary>Gets the IDbConnection instance associated with the facility and entity session.</summary>
    DbConnection DbConnection { get; }

    /// <summary>Gets the IDbTransaction instance associated with the facility and entity session.</summary>
    DbTransaction DbTransaction { get; }

    IDataReader ExecuteSelect(DbCommand command);
    int ExecuteNonQuery(DbCommand command);
    T ExecuteScalar<T>(DbCommand command);

  }

}
