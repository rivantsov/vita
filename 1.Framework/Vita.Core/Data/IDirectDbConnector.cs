using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Data;

namespace Vita.Data {

  /// <summary>Identifies the execution method of the DB command.</summary>
  public enum DbExecutionType {
    Reader,
    NonQuery,
    Scalar,
  }


  /// <summary>Provides direct Db access. You can execute ADO.NET commands (SQL or stored procedures) using this facility.</summary>
  /// <remarks>Use session.GetDirectDbConnection extension method to obtain a connector. </remarks>
  public interface IDirectDbConnector : IDisposable {
    
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
    IDbConnection DbConnection { get; }

    /// <summary>Gets the IDbTransaction instance associated with the facility and entity session.</summary>
    IDbTransaction DbTransaction { get; }
    
    /// <summary>Executes the DB command. The executed command (SQL and statistics) is logged to operation log.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="executionType">The execution method to use.</param>
    /// <param name="resultsReader">The results reader for SELECT commands. Optional. If your provide the reader delegate, 
    /// the statistics (row count) are recorded into the log.</param>
    /// <returns>The result returned by the execution method. </returns>
    /// <remarks>
    /// If an exception is thrown, the current transaction is aborted automatically. However, the connection is not closed
    /// automatically.
    /// </remarks>
    object ExecuteDbCommand(IDbCommand command, DbExecutionType executionType, Func<IDataReader, int> resultsReader = null);
  }

}
