using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Logging;
using Vita.Data.Linq;

namespace Vita.Data.SQLite {

  public class SQLiteDbDriver : DbDriver {

    public const DbFeatures SQLiteFeatures = DbFeatures.TreatBitAsInt | DbFeatures.Views |
                                             DbFeatures.ReferentialConstraints | DbFeatures.Paging | DbFeatures.InsertMany |
                                             DbFeatures.ServerPreservesComments /*views*/
                                             ;
    public const DbOptions DefaultSQLiteDbOptions = DbOptions.UseRefIntegrity | DbOptions.ShareDbModel
                                                  | DbOptions.AutoIndexForeignKeys | DbOptions.AddSchemaToTableNames;

    private bool _autoEnableFK; 

    //Parameterless constructor is needed for tools
    /// <summary>Creates driver instance with enabled Foreign key checks.</summary>
    public SQLiteDbDriver() : this(true) {
      base.TypeRegistry = new SQLiteTypeRegistry(this);
      base.SqlDialect = new SQLiteDbSqlDialect(this); 
    }

    /// <summary>Creates driver instance.</summary>
    public SQLiteDbDriver(bool autoEnableFK) : base(DbServerType.SQLite, SQLiteFeatures) {
      _autoEnableFK = autoEnableFK;
    }

    public override DbOptions GetDefaultOptions() {
      return DefaultSQLiteDbOptions;
    }

    public override IDbConnection CreateConnection(string connectionString) {
      var conn = new SqliteConnection(connectionString);
      // enable FKs; for original SQLite driver, it was a flag in connection string
      // in MS SQLite provider you have to run pragma when you open connection
      if (_autoEnableFK)
        conn.StateChange += Conn_StateChange;
      return conn; 
    }

    // MS SQLite driver does not have conn string parameter for enabling FKs - unlike original SQLite driver; instead you have to run this pragma command
    // when you open the connection
    private void Conn_StateChange(object sender, StateChangeEventArgs e) {
      if(e.CurrentState == ConnectionState.Open) {
        var conn = (IDbConnection)sender; 
        var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        // we need to dispose 
        cmd.Connection = null;
        cmd.Dispose(); 
      }
    }

    public override DbSqlBuilder CreateDbSqlBuilder(DbModel dbModel, QueryInfo queryInfo) {
      return new SQLiteDbSqlBuilder(dbModel, queryInfo); 
    }

    public override DbModelUpdater CreateDbModelUpdater(DbSettings settings) {
      return new SQLiteDbModelUpdater(settings);
    }
    public override DbModelLoader CreateDbModelLoader(DbSettings settings, IActivationLog log) {
      return new SQLiteDbModelLoader(settings, log); 
    }
    public override IDbCommand CreateCommand() {
      return new SqliteCommand();
    }


    /*
      public enum SQLiteErrorCode {
        Unknown = -1,
        Ok = 0,
        Error = 1,
        Internal = 2,
        Perm = 3,
        Abort = 4,
        Busy = 5,
        Locked = 6,
        NoMem = 7,
        ReadOnly = 8,
        Interrupt = 9,
        IoErr = 10,
        Corrupt = 11,
        NotFound = 12,
        Full = 13,
        CantOpen = 14,
        Protocol = 15,
        Empty = 16,
        Schema = 17,
        TooBig = 18,
        Constraint = 19,
        Mismatch = 20,
        Misuse = 21,
        NoLfs = 22,
        Auth = 23,
        Format = 24,
        Range = 25,
        NotADb = 26,
        Notice = 27,
        Warning = 28,
        Row = 100,
        Done = 101,
        NonExtendedMask = 255,
      }

     */
    public override void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
      var sqlEx = dataException.InnerException as SqliteException;
      if(sqlEx == null) return;
      dataException.ProviderErrorNumber = sqlEx.SqliteErrorCode;
      var msg = sqlEx.Message; 
      switch(sqlEx.SqliteErrorCode) {
        case 19: // SqliteErrorCodes.Constraint:
          if(msg.Contains("UNIQUE")) {
            dataException.SubType = DataAccessException.SubTypeUniqueIndexViolation;
            dataException.Data[DataAccessException.KeyDbColumnNames] = GetColumnListFromErrorMessage(msg);
          } else if(msg.Contains("FOREIGN KEY")) {
            dataException.SubType = DataAccessException.SubTypeIntegrityViolation;
          } 
          break; 
      }
    }

    // error message: "SQLite Error 19: 'UNIQUE constraint failed: misc_Driver.LicenseNumber'."
    // notice ending quote and dot
    private string GetColumnListFromErrorMessage(string message) {
      var colPos = message.LastIndexOf(':');
      if(colPos < 0)
        return null;
      var colList = message.Substring(colPos + 1).Replace(" ", string.Empty); //to remove extra spaces 
      //remove extras at the end
      colList = colList.TrimEnd('\'', '.');
      return colList; 
    }


  }
}
