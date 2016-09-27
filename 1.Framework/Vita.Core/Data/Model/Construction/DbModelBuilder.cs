using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Reflection;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Linq;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Data.Driver;

namespace Vita.Data.Model {

  /// <summary>Builds Db model from entity model.</summary>
  public class DbModelBuilder {
    EntityModel _entityModel;
    DbModel _dbModel; 
    DbDriver _driver;
    DbModelConfig _config;
    int _tableKeyIndex; //is used to generate unique index names
    MemoryLog _log;
    DbSqlBuilder _dbSqlBuilder; 


    public DbModelBuilder(EntityModel entityModel, DbModelConfig config, MemoryLog log) {
      _entityModel = entityModel;
      _config = config;
      _log = log;
      _driver = _config.Driver;
    }

    public DbModel Build() {
      // create model
      _dbModel = new DbModel(_entityModel.App, _config);
      _dbSqlBuilder = _driver.CreateDbSqlBuilder(_dbModel);
      _driver.OnDbModelConstructing(_dbModel);
      BuildTables();
      CreateTableKeys();
      SetupOrderBy();
      //ref constraints are created in a separate loop, after creating PKs
      BuildRefConstraints();
      CheckObjectNames();
      CompileViews();
      BuildCrudCommands();
      BuildSequences(); 
      _driver.OnDbModelConstructed(_dbModel);
      CheckErrors();
      return _dbModel;
    }//method

    private void CheckErrors() {
      if(_log.HasErrors()) {
        var errors = _log.GetAllAsText();
        throw new StartupFailureException("DbModel construction failed.", errors);
      }
    }

    private void LogError(string message, params object[] args) {
      _log.Error(message, args);
    }

    private bool IsActive(EntityArea area) {
      return true; 
      /*
      if(!_driver.Supports(DbFeatures.Schemas))
        return true; 
      return _dbModel.ContainsSchema(area.Name); 
       */ 
    }

    private void CompileViews() {
      var hasher = new SqlSourceHasher(); 
      if (!_dbModel.Driver.Supports(DbFeatures.Views))
        return; 
      var views = _dbModel.Tables.Where(t => t.Kind == EntityKind.View).ToList(); 
      var engine = new Vita.Data.Linq.Translation.LinqEngine(_dbModel);
      foreach(var viewTbl in views) {
        var translatedCmd = engine.Translate(viewTbl.Entity.ViewDefinition.Command);
        viewTbl.ViewHash = hasher.ComputeHash(translatedCmd.Sql);
        viewTbl.ViewSql = hasher.GetHashLine(viewTbl.ViewHash) + Environment.NewLine + translatedCmd.Sql;
        //Save hash in DbVersionInfo
        var hashKey = DbModel.GetViewKey(viewTbl);
        _dbModel.VersionInfo.Values[hashKey] = viewTbl.ViewHash;
      }
    }

    private void BuildCrudCommands() {
      bool useSPs = _driver.Supports(DbFeatures.StoredProcedures) && _config.Options.IsSet(DbOptions.UseStoredProcs);
      foreach (var entityCommand in _entityModel.GetCrudCommands()) {
        if (entityCommand.TargetEntityInfo.Kind == EntityKind.View && !_dbModel.Driver.Supports(DbFeatures.Views))
          continue;
        // We try to match commands by descrTag; it sometimes happens that two entity commands are identical and have the same underlying db command
        var descrTag = _dbSqlBuilder.GetDbCommandDescriptiveTag(entityCommand); 
        var cmd = _dbModel.GetCommandByTag(descrTag);
        if (cmd != null) { //if it exists already, register it with other entity command
          _dbModel.RegisterDbObject(entityCommand, cmd); 
          continue;
        }
        cmd = BuildCommand(_dbSqlBuilder, entityCommand);
        if (cmd == null)
          continue; 
        
        if(useSPs && !cmd.IsTemplatedSql) {
          _dbSqlBuilder.ConvertToStoredProc(cmd);
          cmd.CommandType = CommandType.StoredProcedure; 
        }
      }
    }

    private void BuildSequences() {
      if (!_driver.Supports(DbFeatures.Sequences))
        return; 
      foreach(var m in _entityModel.App.Modules)
        foreach (var seq in m.Sequences) {
          var dbSeqInfo = new DbSequenceInfo(this._dbModel, seq);
          dbSeqInfo.GetNextValueCommand = _dbSqlBuilder.BuildSqlSequenceGetNextCommand(dbSeqInfo);
          _dbModel.Sequences.Add(dbSeqInfo); 
        }
    }//method


    public DbCommandInfo BuildCommand(DbSqlBuilder dbSqlBuilder, EntityCommand entityCommand) {
      switch(entityCommand.Kind) {
        case EntityCommandKind.SelectAll:
          return dbSqlBuilder.BuildSelectAllCommand(entityCommand);
        case EntityCommandKind.SelectAllPaged:
          return dbSqlBuilder.BuildSelectAllPagedCommand(entityCommand);
        case EntityCommandKind.SelectByKey:
          return dbSqlBuilder.BuildSelectByKeyCommand(entityCommand);
        case EntityCommandKind.SelectByKeyArray:
          return dbSqlBuilder.BuildSelectByKeyArrayCommand(entityCommand);
        case EntityCommandKind.SelectByKeyManyToMany:
          return dbSqlBuilder.BuildSelectManyToManyCommand(entityCommand);
        case EntityCommandKind.Insert:
          return dbSqlBuilder.BuildSqlInsertCommand(entityCommand);
        case EntityCommandKind.Update:
        case EntityCommandKind.PartialUpdate:
          return dbSqlBuilder.BuildSqlUpdateCommand(entityCommand);
        case EntityCommandKind.Delete:
          return dbSqlBuilder.BuildSqlDeleteCommand(entityCommand);
        default:
          // it is custom command - should be compiled on the fly
          return null;
      }
    }


    //Create tables and regular "value" columns
    private void BuildTables() {
      var supportsViews = _dbModel.Driver.Supports(DbFeatures.Views);
      foreach (var entityInfo in _entityModel.Entities) {
        if (!IsActive(entityInfo.Area))
          continue;
        if (entityInfo.Kind == EntityKind.View && !supportsViews)
          continue; 

        var tableName = ConstructDefaultTableName(entityInfo);
        var objType = entityInfo.Kind == EntityKind.Table ? DbObjectType.Table : DbObjectType.View;
        var schema = _config.GetSchema(entityInfo.Area);
        var table = new DbTableInfo(_dbModel, schema, tableName, entityInfo, objType);
        // Check materialized view - automatically set the flag if there are indexes on the view
        if (entityInfo.Kind == EntityKind.View) {
          if (_driver.Supports(DbFeatures.MaterializedViews) && entityInfo.ViewDefinition.Options.IsSet(DbViewOptions.Materialized))
            table.IsMaterializedView = true;
        }
        //create Value columns 
        foreach (var member in entityInfo.Members)
          if (member.Kind == MemberKind.Column)
            CreateDbColumn(table, member);
        //reorder DbColumns, make PK appear first
        var pkColumns = table.Columns.Where(c => c.Member.Flags.IsSet(EntityMemberFlags.PrimaryKey)).ToList();
        foreach (var pkCol in pkColumns) {
          table.Columns.Remove(pkCol);
          table.Columns.Insert(0, pkCol);
        }
      }//foreach entityInfo
      CheckErrors();
    }

    private DbColumnInfo CreateDbColumn(DbTableInfo table, EntityMemberInfo member) {
      bool isError = false; 
      var colName = member.ColumnName;
      string colDefault = member.ColumnDefault; //comes from attribute
      if (colDefault != null && member.DataType == typeof(string) && !colDefault.StartsWith("'"))
        colDefault = colDefault.Quote();

      var dbTypeInfo = _driver.TypeRegistry.GetDbTypeInfo(member, _log);
      if (dbTypeInfo == null) {
        isError = true;
        LogError("Driver failed to match db type for data type {0}, member {1}.{2}", member.DataType, member.Entity.FullName, member.MemberName);
        // do not throw, continue to find more errors
        //Util.Throw("Driver failed to match db type for data type {0}, member {1}.{2}", member.DataType, member.Entity.FullName, member.MemberName);
        return null; 
      }
      var dbColumn = new DbColumnInfo(member, table, colName, dbTypeInfo);
      if (!string.IsNullOrEmpty(colDefault))
        dbColumn.DefaultExpression = colDefault;
      if (member.AutoValueType == AutoType.Identity)
        dbColumn.Flags |= DbColumnFlags.Identity | DbColumnFlags.NoUpdate | DbColumnFlags.NoInsert;
      if (member.Flags.IsSet(EntityMemberFlags.Secret))
        dbColumn.Flags |= DbColumnFlags.NoUpdate; //updated only thru custom update method
      if (member.Flags.IsSet(EntityMemberFlags.NoDbInsert))
        dbColumn.Flags |= DbColumnFlags.NoInsert;
      if(member.Flags.IsSet(EntityMemberFlags.NoDbUpdate))
        dbColumn.Flags |= DbColumnFlags.NoUpdate;
      if(isError)
        dbColumn.Flags |= DbColumnFlags.Error;
      if (member.Flags.IsSet(EntityMemberFlags.UnlimitedSize) && _driver.Supports(DbFeatures.ForceNullableMemo)) //case for SQL CE
        dbColumn.Flags |= DbColumnFlags.Nullable;
      return dbColumn;
    }//method

    private void CreateTableKeys() {
      var createIndexesOnForeignKeys = _driver.Supports(DbFeatures.NoIndexOnForeignKeys) 
                                       && _config.Options.IsSet(DbOptions.AutoIndexForeignKeys);
      var supportsClustedIndex = _driver.Supports(DbFeatures.ClusteredIndexes);
      var supportsIndexedViews = _driver.Supports(DbFeatures.MaterializedViews);

      foreach (var table in _dbModel.Tables) {
        if (table.Kind == EntityKind.View && !supportsIndexedViews)
          continue; 
        var entity = table.Entity;
        _tableKeyIndex = 0; //counter used to create unique index names
        foreach (var entityKey in entity.Keys) {
          if (table.Kind == EntityKind.View && entityKey.KeyType == KeyType.ForeignKey)
            continue; //views do not have foreign keys; view entities can reference other entities, but this relationship is not represented in database
          var keyCols = new List<DbKeyColumnInfo>();
          //Find columns and add them to the key
          foreach (var keyMember in entityKey.ExpandedKeyMembers) {
            var col = table.Columns.First(c => c.Member == keyMember.Member); 
            keyCols.Add(new DbKeyColumnInfo(col, keyMember));
          }//foreach member
          var inclCols = new List<DbColumnInfo>();
          foreach(var incMember in entityKey.ExpandedIncludeMembers) {
            var col = table.Columns.First(c => c.Member == incMember);
            inclCols.Add(col);
          }//foreach member

          //Create DBKey
          var keyType = entityKey.KeyType;
          if (!supportsClustedIndex)
            keyType &= ~KeyType.Clustered;
          var keyName = ConstructDbKeyName(table, keyType, keyCols, entityKey.Name);
          var dbKey = new DbKeyInfo(keyName, table, keyType, entityKey);
          dbKey.KeyColumns.AddRange(keyCols);
          dbKey.IncludeColumns.AddRange(inclCols);
          //check filter
          if(!string.IsNullOrWhiteSpace(entityKey.Filter))
            dbKey.Filter = ProcessKeyFilter(entityKey.Filter, table); 
          //Assign PK if it is PK
          if (keyType.IsSet(KeyType.PrimaryKey)) 
            table.PrimaryKey = dbKey;
          if (keyType.IsSet(KeyType.Clustered))
            foreach (var keycol in dbKey.KeyColumns)
              keycol.Column.Flags |= DbColumnFlags.ClusteredIndex;
        }//foreach key
        // Check primary key columns, mark them as no-update
        if (table.Kind == EntityKind.View)
          continue; 
        foreach (var keyCol in table.PrimaryKey.KeyColumns)
          keyCol.Column.Flags |= DbColumnFlags.NoUpdate | DbColumnFlags.PrimaryKey;
        
        //Create indexes on foreign keys
        if (createIndexesOnForeignKeys) {
          //copy FKeys into a separate list, to avoid trouble when adding indexes to collection that we are iterating
          var foreignKeys = table.Keys.Where(k => k.KeyType.IsSet(KeyType.ForeignKey)).ToList();
          foreach (var key in foreignKeys) {
            //Check if there is already index with the same starting columns
            var matchingIndex = FindMatchingIndexForForeignKey(key);
            if (matchingIndex != null)
              continue;
            // construct index name, strip "IX_" prefix and replace it with "IX_FK_"
            var indexName = "IX_FK_" + ConstructDbKeyName(table, KeyType.Index, key.KeyColumns, key.Name).Substring(3);
            var dbIndex = new DbKeyInfo(indexName, table, KeyType.Index);
            dbIndex.KeyColumns.AddRange(key.KeyColumns);
          }
        }//if createIndexes

      }//foreach table
      CheckErrors(); 
    }

    //Replace references to properties (ex: {SomeProp}) with column references
    // Filters are supported by MS SQL only. MS SQL parses and normalizes the filter, enclosing column names in [], and enclosing filter in ()
    // To match the final result in database - so that db schema comparison works correctly - we do the same with filter definition
    private string ProcessKeyFilter(string filter, DbTableInfo table) {
      if(filter == null || !filter.Contains('{'))
        return filter; 
      foreach(var col in table.Columns) {
        var name = "{" + col.Member.MemberName + "}";
        if(!filter.Contains(name))
          continue;
        var colRef = '[' + col.ColumnName + ']';
        filter = filter.Replace(name, colRef);
      }
      filter = "(" + filter + ")";
      return filter; 
    }

    // Finds an existing index that can be used to support (speed-up) the foreign key. It can be index, or primary key
    // If no index is found, system would create an extra index for the foreign key (when corresponding option is set)
    private DbKeyInfo FindMatchingIndexForForeignKey(DbKeyInfo key) {
      var table = key.Table;
      foreach (var index in key.Table.Keys) {
        //We are interested only in indexes or primary keys (or clustered primary keys)
        if (!index.KeyType.IsSet(KeyType.Index | KeyType.PrimaryKey)) 
          continue;  
        if (index.KeyColumns.Count < key.KeyColumns.Count) //it is not a match if it has fewer columns
          continue; 
        bool match = true;
        for (int i = 0; i < key.KeyColumns.Count; i++)
          match &= (key.KeyColumns[i].Column == index.KeyColumns[i].Column);
        if (match)
          return index;
      }//foreach
      return null;   
    }

    private void BuildRefConstraints() {
      foreach (var table in _dbModel.Tables) {
        if (table.Kind != EntityKind.Table)
          continue; 
        var entity = table.Entity;
        foreach (var member in entity.Members) {
          if (member.Kind != MemberKind.EntityRef)
            continue;
          var refInfo = member.ReferenceInfo;
          if (!IsActive(refInfo.ToKey.Entity.Area)) 
            continue; //target entity/table is not in this db model; so no foreign key link 
          var fromDbKey = _dbModel.LookupDbObject<DbKeyInfo>(refInfo.FromKey);
          var toDbKey = _dbModel.LookupDbObject<DbKeyInfo>(refInfo.ToKey);
          var cascadeDelete = member.Flags.IsSet(EntityMemberFlags.CascadeDelete);
          var dbRC = new DbRefConstraintInfo(_dbModel, fromDbKey, toDbKey, cascadeDelete, refInfo);
          table.RefConstraints.Add(dbRC);
        }
      }//foreach entityInfo
      CheckErrors();
    }

    private void SetupOrderBy() {
      foreach (var table in _dbModel.Tables) {
        var orderBy = table.Entity.DefaultOrderBy;
        if (orderBy != null) 
          table.DefaultOrderBy = ConstructOrderBy(orderBy, table);
        foreach(var key in table.Keys)
          // some keys do not have entity key (indexes)
          if (key.EntityKey != null && key.EntityKey.OrderByForSelect != null) {
            key.OrderByForSelect = ConstructOrderBy(key.EntityKey.OrderByForSelect, table); 
          }
      }//foreach 
      CheckErrors(); 
    }//method

    private IList<DbKeyColumnInfo> ConstructOrderBy(IList<EntityKeyMemberInfo> keyMembers, DbTableInfo table) {
      if(keyMembers == null || keyMembers.Count == 0)
        return null; 
      var orderByList = new List<DbKeyColumnInfo>(); 
      var ent = table.Entity;
      foreach (var keyMember in keyMembers) {
        var col = table.Columns.FirstOrDefault(c=>c.Member == keyMember.Member); 
        if (col == null) {
          // This might happen in OrderBy on many-to-many list, when we order by member in target table 
          // ex: Author.Books, order by book.PublishedOn; in this case primary table is link table IBookAuthor;
          // otherTbl is IBook
          var otherTbl = _dbModel.GetTable(keyMember.Member.Entity.EntityType); 
          if (otherTbl != null)
            col = otherTbl.Columns.FirstOrDefault(c => c.Member == keyMember.Member);
        }
        if (col == null)
          LogError("Failed to build ORDER BY list for table {0}: cannot find column for member {1}.", table.TableName, keyMember.Member.MemberName);
        var dbKeyColumn = new DbKeyColumnInfo(col, keyMember);
        orderByList.Add(dbKeyColumn);
      }
      if (orderByList.Count == 0)
        return null; 
      return orderByList; 
    }

    //default, we run thru naming policy later
    private string ConstructDefaultTableName(EntityInfo entity) {
      if (!string.IsNullOrWhiteSpace(entity.TableName)) 
        return entity.TableName;
      switch(entity.Kind) {
        case EntityKind.View:
          return this._config.NamingPolicy.ViewPrefix + entity.Name;
        case EntityKind.Table:
        default:
          return this._config.NamingPolicy.TablePrefix + entity.Name;
      }
    }

    private void CheckObjectNames() {
      foreach (var table in _dbModel.Tables) {
        if (_config.Options.IsSet(DbOptions.PluralizeTableNames))
          table.TableName = StringHelper.Pluralize(table.TableName); 
        table.TableName = _config.NamingPolicy.CheckName(DbNamedObjectType.Table, table.TableName, table);
        foreach(var col in table.Columns)
          col.ColumnName = _config.NamingPolicy.CheckName(DbNamedObjectType.Column, col.ColumnName, col);
        foreach (var key in table.Keys)
          key.Name = _config.NamingPolicy.CheckName(DbNamedObjectType.Key, key.Name, key);
      }
    }//method


    private string ConstructDbKeyName(DbTableInfo table, KeyType keyType, IList<DbKeyColumnInfo> keyColumns, string entityKeyName) {
      string keyName = entityKeyName;
      //check that it does not exist; if it does, append index to it.
      var existing = table.Keys.FirstOrDefault(c => c.Name == keyName);
      if (existing != null)
        keyName = keyName + (_tableKeyIndex++); 
      return keyName; 
    }

  }//class
}//namespace
