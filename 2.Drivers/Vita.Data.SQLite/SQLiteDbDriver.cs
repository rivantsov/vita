using System;
using System.Collections.Generic;
using System.Data;

using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Data.Linq;

using System.Data.SQLite;

namespace Vita.Data.SQLite {

  public class SQLiteDbDriver : DbDriver {

    public const DbFeatures SQLiteFeatures = DbFeatures.TreatBitAsInt | DbFeatures.Views |
                                             DbFeatures.ReferentialConstraints | DbFeatures.Paging | DbFeatures.InsertMany |
                                             DbFeatures.ServerPreservesComments /*views*/
                                             ;
    public const DbOptions DefaultSQLiteDbOptions = DbOptions.UseRefIntegrity | DbOptions.ShareDbModel
                                                  | DbOptions.AutoIndexForeignKeys | DbOptions.AddSchemaToTableNames;

    //Parameterless constructor is needed for tools
    /// <summary>Creates driver instance with enabled Foreign key checks.</summary>
    public SQLiteDbDriver() : base(DbServerType.SQLite, SQLiteFeatures) {
      base.TypeRegistry = new SQLiteTypeRegistry(this);
      base.SqlDialect = new SQLiteDbSqlDialect(this);
    }

    public override DbOptions GetDefaultOptions() {
      return DefaultSQLiteDbOptions;
    }

    public override IDbConnection CreateConnection(string connectionString) {
      return new SQLiteConnection(connectionString);
    }
    public override IDbCommand CreateCommand() {
      return new SQLiteCommand();
    }

    public override DbLinqSqlBuilder CreateLinqSqlBuilder(DbModel dbModel, LinqCommand command) {
      return new SQLiteLinqSqlBuilder(dbModel, command); 
    }
    public override DbCrudSqlBuilder CreateCrudSqlBuilder(DbModel dbModel) {
      return new SQLiteCrudSqlBuilder(dbModel);
    }

    public override DbModelUpdater CreateDbModelUpdater(DbSettings settings) {
      return new SQLiteDbModelUpdater(settings);
    }
    public override DbModelLoader CreateDbModelLoader(DbSettings settings, IActivationLog log) {
      return new SQLiteDbModelLoader(settings, log); 
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
      var sqlEx = dataException.InnerException as SQLiteException;
      if(sqlEx == null) return;
      dataException.ProviderErrorNumber = sqlEx.ErrorCode;
      var msg = sqlEx.Message; 
      switch(sqlEx.ErrorCode) {
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
