using System;
using System.Collections.Generic;
using System.Text;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Data.Sql;
using Vita.Data.Runtime;

namespace Vita.Data.Sql {

  public class SqlFactory {
    DbModel _dbModel;
    DbDriver _driver;
    SqlCache _sqlCache; 
    LinqEngine _linqEngine;
    DbCrudSqlBuilder _crudSqlBuilder; 

    public SqlFactory(DbModel dbModel) {
      _dbModel = dbModel;
      _driver = _dbModel.Driver;
      _sqlCache = new SqlCache(10000); 
      _linqEngine = new LinqEngine(_dbModel);
      _crudSqlBuilder = _dbModel.Driver.CreateCrudSqlBuilder(_dbModel);
    }

    public SqlStatement GetLinqSql(LinqCommand command) {
      if(command.Info == null)
        LinqCommandAnalyzer.Analyze(_dbModel.EntityModel, command);
      var cacheKey = command.Info.CacheKey;
      var stmt = _sqlCache.Lookup(cacheKey);
      if(stmt != null)
        return stmt;
      stmt = _linqEngine.Translate(command);
      _driver.SqlDialect.ReviewSqlStatement(stmt, command);
      if(!command.Info.Options.IsSet(QueryOptions.NoQueryCache))
        _sqlCache.Add(cacheKey, stmt);
      return stmt;
    }

    public SqlStatement GetCrudSqlForSingleRecord(DbTableInfo table, EntityRecord record) {
      var cacheKey = SqlCacheKey.CreateForCrud(table.Entity, record.Status, record.MaskMembersChanged);
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
      _driver.SqlDialect.ReviewSqlStatement(sql, table); 
      _sqlCache.Add(cacheKey, sql); 
      return sql; 
    }


    public SqlStatement GetCrudDeleteMany(DbTableInfo table) {
      var cacheKey = SqlCacheKey.CreateForDeleteMany(table.Entity);
      var sql = _sqlCache.Lookup(cacheKey);
      if(sql != null)
        return sql; 
      sql = _crudSqlBuilder.BuildCrudDeleteMany(table);
      _driver.SqlDialect.ReviewSqlStatement(sql, table);
      _sqlCache.Add(cacheKey, sql);
      return sql; 
    }

    // Insert-many are never cached - these are custom-built each time
    public SqlStatement  GetCrudInsertMany(DbTableInfo table, IList<EntityRecord> records, IColumnValueFormatter formatter) {
      var sql = _crudSqlBuilder.BuildCrudInsertMany(table, records, formatter);
      _driver.SqlDialect.ReviewSqlStatement(sql, table);
      return sql; 
    }

  } //class
}
