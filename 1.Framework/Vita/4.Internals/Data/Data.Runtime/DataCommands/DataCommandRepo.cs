using System;
using System.Collections.Generic;
using System.Text;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Data.SqlGen;

namespace Vita.Data.Runtime {

  public class DataCommandRepo {
    DbModel _dbModel;
    DbDriver _driver;
    SqlStatementCache _sqlCache; 
    LinqEngine _linqEngine;
    DbSqlBuilder _crudSqlBuilder; 

    public DataCommandRepo(DbModel dbModel) {
      _dbModel = dbModel;
      _driver = _dbModel.Driver;
      _sqlCache = new SqlStatementCache(10000); 
      _linqEngine = new LinqEngine(_dbModel);
      _crudSqlBuilder = _dbModel.Driver.CreateDbSqlBuilder(_dbModel, null);
    }

    public SqlStatement GetLinqSelect(LinqCommand command) {
      if(command.Info == null)
        LinqCommandAnalyzer.Analyze(_dbModel.EntityModel, command);
      var cacheKey = command.Info.CacheKey;
      var stmt = _sqlCache.Lookup(cacheKey);
      if(stmt != null)
        return stmt;
      stmt = _linqEngine.Translate(command);
      if(!command.Info.Options.IsSet(QueryOptions.NoQueryCache))
        _sqlCache.Add(cacheKey, stmt);
      return stmt;
    }

    public SqlStatement GetCrudSqlForSingleRecord(DbTableInfo table, EntityRecord record) {
      var cacheKey = SqlCacheKey.CreateForCrud(table.Entity, record.Status, record.ChangedMembersMask);
      SqlStatement sql = _sqlCache.Lookup(cacheKey);
      if(sql != null)
        return sql; 
      // build it
      switch(record.Status) {
        case EntityStatus.New:
          sql = _crudSqlBuilder.BuildCrudInsertOne(table, record);
          break;
        case EntityStatus.Modified:
          sql = _crudSqlBuilder.BuildCrudUpdateOne(table, record);
          break;
        case EntityStatus.Deleting:
          sql = _crudSqlBuilder.BuildCrudDeleteOne(table); 
          break; 
      }
      _sqlCache.Add(cacheKey, sql); 
      return sql; 
   }

    public SqlStatement GetLinqNonQuery(LinqCommand command) {
      return _linqEngine.TranslateNonQuery(command);
    }

    public SqlStatement GetCrudDeleteMany(DbTableInfo table) {
      return _crudSqlBuilder.BuildCrudDeleteMany(table); 
    }

    public SqlStatement  GetCrudInsertMany(DbTableInfo table, IList<EntityRecord> records, IColumnValueFormatter formatter) {
      return _crudSqlBuilder.BuildCrudInsertMany(table, records, formatter);
    }

  } //class
}
