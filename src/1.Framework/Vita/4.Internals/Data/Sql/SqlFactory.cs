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

  // Provides a source of SQL statements for linq/crud commands. Uses SqlBuilders and caches generated SQLs.
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
      // lookup in cache
      var stmt = GetCachedLinqSql(command);
      if(stmt != null)
        return stmt;
      // not in cache - translate
      stmt = _linqEngine.Translate(command);
      _driver.SqlDialect.ReviewSqlStatement(stmt, command);
      if(!command.Options.IsSet(QueryOptions.NoQueryCache))
        _sqlCache.Add(command.SqlCacheKey, stmt);
      return stmt;
    }

    public SqlStatement GetCrudSqlForSingleRecord(DbTableInfo table, EntityRecord record) {
      var cacheKey = SqlCacheKeyBuilder.BuildCrudKey(record.Status, record.EntityInfo.Name, record.MaskMembersChanged);
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
      var cacheKey = SqlCacheKeyBuilder.BuildCrudDeleteManyKey(table.Entity.Name);
      var sql = _sqlCache.Lookup(cacheKey);
      if(sql != null)
        return sql; 
      sql = _crudSqlBuilder.BuildCrudDeleteMany(table);
      _driver.SqlDialect.ReviewSqlStatement(sql, table);
      _sqlCache.Add(cacheKey, sql);
      return sql; 
    }

    // Insert-many are never cached - these are custom-built each time
    public SqlStatement  GetCrudInsertMany(DbTableInfo table, IList<EntityRecord> records, DataCommandBuilder commandBuilder) {
      var sql = _crudSqlBuilder.BuildCrudInsertMany(table, records, commandBuilder); //commandBuilder is ICommandValueFormatter
      _driver.SqlDialect.ReviewSqlStatement(sql, table);
      return sql; 
    }

    // Helper methods 

    private SqlStatement GetCachedLinqSql(LinqCommand command) {
      if(command.Options.IsSet(QueryOptions.NoQueryCache))
        return null;
      Util.Check(!string.IsNullOrEmpty(command.SqlCacheKey), "Fatal: SQL cache key is not set for LINQ query: {0}", command);
      var sql = _sqlCache.Lookup(command.SqlCacheKey);
      return sql;
    }

  } //class
}
