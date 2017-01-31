using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlServerCe;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Logging;

namespace Vita.Data.SqlCe {

  public class SqlCeDbDriver : DbDriver { 
    public const DbOptions DefaultDbOptions = DbOptions.Default | DbOptions.AutoIndexForeignKeys;
    //SQL CE does not allow multiple SQL statements in one call, so batched updates are not supported.
    public const DbFeatures SqlCeFeatures = DbFeatures.ReferentialConstraints | DbFeatures.Paging | DbFeatures.DefaultCaseInsensitive
                                            | DbFeatures.OrderedColumnsInIndexes | DbFeatures.NoIndexOnForeignKeys 
                                            | DbFeatures.SkipTakeRequireOrderBy
                                            | DbFeatures.ForceNullableMemo | DbFeatures.NoMemoInDistinct | DbFeatures.TreatBitAsInt;

    // constructor and initialization
    public SqlCeDbDriver() : base(DbServerType.SqlCe, SqlCeFeatures){
      this.DynamicSqlParameterPrefix = "@";
      base.CommandCallFormat = "EXEC {0} {1};";
    }


    protected override DbTypeRegistry CreateTypeRegistry() {
      return new SqlCeTypeRegistry(this); 
    }
    public override IDbConnection CreateConnection(string connectionString) {
      return new SqlCeConnection(connectionString);
    }

    public override DbSqlBuilder CreateDbSqlBuilder(DbModel dbModel) {
      return new SqlCeDbSqlBuilder(dbModel);
    }
    public override DbModelUpdater CreateDbModelUpdater(DbSettings settings) {
      return new SqlCeDbModelUpdater(settings);
    }
    public override DbModelLoader CreateDbModelLoader(DbSettings settings, MemoryLog log) {
      return new SqlCeDbModelLoader(settings, log);
    }

    public override LinqSqlProvider CreateLinqSqlProvider(DbModel dbModel) {
      return new SqlCeLinqSqlProvider(dbModel);
    }

    public override void ClassifyDatabaseException(DataAccessException dataException, IDbCommand command = null) {
      var sqlEx = dataException.InnerException as SqlCeException;
      if (sqlEx == null) 
        return;
      dataException.ProviderErrorNumber = sqlEx.NativeError;
      switch (sqlEx.NativeError) {
        case 25016:  //unique index violation
          dataException.SubType = DataAccessException.SubTypeUniqueIndexViolation;
          dataException.Data[DataAccessException.KeyDbKeyName] = GetIndexName(sqlEx.Message);
          break;
        case 25025: //integrity violation
          dataException.SubType = DataAccessException.SubTypeIntegrityViolation;
          break; 
      }
    }

    private string GetIndexName(string message) {
      try {
        // Sample message: A duplicate value cannot be inserted into a unique index. [ Table name = Publisher,Constraint name = IXU_Publisher_Name ]
        var constrNameTag = "Constraint name =";
        var tagPos = message.IndexOf(constrNameTag);
        var start = tagPos + constrNameTag.Length; 
        var end = message.IndexOf("]", start);
        var indexName = message.Substring(start, end - start).Trim();
        return indexName;
      } catch (Exception ex) {
        return "(failed to identify index in message: " + ex.Message + ")"; //
      }
    }

    public override IDbDataParameter AddParameter(IDbCommand command, DbParamInfo prmInfo) {
      var prm = (SqlCeParameter)base.AddParameter(command, prmInfo);
      prm.SqlDbType = (SqlDbType) prmInfo.TypeInfo.VendorDbType.VendorDbType;
      return prm;
    }

    public override void OnDbModelConstructed(DbModel dbModel) {
      // NText columns must be nullable
      foreach (var table in dbModel.Tables) {
        foreach (var col in table.Columns) {
          if (col.TypeInfo.DbType == DbType.String && col.TypeInfo.Size < 0)
            col.Flags |= DbColumnFlags.Nullable;
        }
        // SqlCe does not support filters; this may mess up unique keys on nullable columns
        // drop Unique key for these columns
        foreach(var key in table.Keys)
          if (key.KeyType.IsSet(KeyType.Index) && key.KeyType.IsSet(KeyType.Unique)) {
            var nullableUniqueWithFilter = !string.IsNullOrWhiteSpace(key.Filter) &&
                key.KeyColumns.All(kc => kc.Column.Flags.IsSet(DbColumnFlags.Nullable));
            if (nullableUniqueWithFilter) //drop Unique
              key.KeyType &= ~KeyType.Unique; 
          }
      }
      base.OnDbModelConstructed(dbModel);
    }
  }//class

}
