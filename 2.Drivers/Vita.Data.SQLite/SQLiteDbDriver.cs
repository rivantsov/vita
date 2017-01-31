using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Data.SQLite {

  public class SQLiteDbDriver : DbDriver  {
    public const DbFeatures SQLiteFeatures = DbFeatures.TreatBitAsInt | DbFeatures.Views
                                                    | DbFeatures.ReferentialConstraints | DbFeatures.Paging;
    public const DbOptions DefaultSQLiteDbOptions = DbOptions.Default | DbOptions.AutoIndexForeignKeys;

    public SQLiteDbDriver() : base(DbServerType.Sqlite, SQLiteFeatures) {
      base.DynamicSqlParameterPrefix = "@";
      base.CommandCallFormat = null;
      base.BatchBeginTransaction = "BEGIN;";
      base.BatchCommitTransaction = "COMMIT;";
    }

    protected override DbTypeRegistry CreateTypeRegistry() {
      return new SQLiteTypeRegistry(this);   
    }

    public override IDbConnection CreateConnection(string connectionString) {
      var conn = new SQLiteConnection(connectionString);
      conn.Flags &= ~SQLiteConnectionFlags.BindDateTimeWithKind;
      return conn; 
    }

    public override LinqSqlProvider CreateLinqSqlProvider(Model.DbModel dbModel) {
      return new SQLiteLinqSqlProvider(dbModel); 
    }
    public override DbSqlBuilder CreateDbSqlBuilder(Model.DbModel dbModel) {
      return new SQLiteDbSqlBuilder(dbModel); 
    }

    public override DbModelUpdater CreateDbModelUpdater(DbSettings settings) {
      return new SQLiteDbModelUpdater(settings);
    }

    public override DbModelLoader CreateDbModelLoader(DbSettings settings, MemoryLog log) {
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
      switch(sqlEx.ResultCode) {
        case SQLiteErrorCode.Constraint:
          if(msg.Contains("UNIQUE")) {
            dataException.SubType = DataAccessException.SubTypeUniqueIndexViolation;
            dataException.Data[DataAccessException.KeyDbColumnNames] = GetColumnListFromErrorMessage(msg);
          } else if(msg.Contains("FOREIGN KEY")) {
            dataException.SubType = DataAccessException.SubTypeIntegrityViolation;
          } 
          break; 
      }
    }

    private string GetColumnListFromErrorMessage(string message) {
      var colPos = message.LastIndexOf(':');
      if(colPos < 0)
        return null;
      var colList = message.Substring(colPos + 1).Replace(" ", string.Empty); //to remove extra spaces 
      return colList; 
    }

  }
}
