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
    LinqEngine _linqEngine;
    DbSqlBuilder _crudSqlBuilder; 

    public DataCommandRepo(DbModel dbModel) {
      _dbModel = dbModel;
      _driver = _dbModel.Driver; 
      _linqEngine = new LinqEngine(_dbModel);
      _crudSqlBuilder = _dbModel.Driver.CreateDbSqlBuilder(_dbModel, null);
    }

    public SqlStatement GetSelect(EntityCommand command) {
      var sql = _linqEngine.Translate(command);
      return sql; 
    }

    public SqlStatement GetCrudNonQuery(DbTableInfo table, EntityRecord record, ISqlValueFormatter valueFormatter) {
      SqlStatement sql = null;
      switch(record.Status) {
        case EntityStatus.New:
          sql = _crudSqlBuilder.BuildCrudInsertOne(table, record);
          break;
        case EntityStatus.Modified:
          sql = _crudSqlBuilder.BuildCrudUpdateOne(table, record, valueFormatter);
          break;
        case EntityStatus.Deleting:
          sql = _crudSqlBuilder.BuildCrudDeleteOne(table); 
          break; 
      }
      return sql; 
   }

    public SqlStatement GetLinqNonQuery(EntityCommand command) {
      return _linqEngine.TranslateNonQuery(command);
    }

    public SqlStatement GetCrudDeleteMany(DbTableInfo table, IList<EntityRecord> records) {
      return _crudSqlBuilder.BuildCrudDeleteMany(table, records); 
    }

    public SqlStatement  GetCrudInsertMany(DbTableInfo table, IList<EntityRecord> records, ISqlValueFormatter formatter) {
      return _crudSqlBuilder.BuildCrudInsertMany(table, records, formatter);
    }

  } //class
}
