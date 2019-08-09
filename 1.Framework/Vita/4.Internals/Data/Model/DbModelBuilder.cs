using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Reflection;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Entities.Logging;
using Vita.Data.Linq;
using Vita.Data.Sql;
using Vita.Data.Runtime;
using Vita.Entities.Runtime;
using Vita.Data.Driver.TypeSystem;
using System.Linq.Expressions;

namespace Vita.Data.Model {

  /// <summary>Builds Db model from entity model.</summary>
  public class DbModelBuilder {
    EntityModel _entityModel;
    DbModel _dbModel; 
    DbDriver _driver;
    DbModelConfig _dbModelConfig;
    IDbNamingPolicy _namingPolicy; 
    ILog _log;

    public DbModelBuilder(EntityModel entityModel, DbModelConfig config, ILog log) {
      _entityModel = entityModel;
      _dbModelConfig = config;
      _namingPolicy = _dbModelConfig.NamingPolicy; 
      _log = log;
      _driver = _dbModelConfig.Driver;
    }

    public DbModel Build() {
      // create model
      _dbModel = new DbModel(_entityModel.App, _dbModelConfig);
      _driver.OnDbModelConstructing(_dbModel);
      BuildTables();
      CreateTableKeys();
      //ref constraints are created in a separate loop, after creating PKs
      BuildRefConstraints();
      FinalizeObjectNames();
      CompleteTablesSetup(); 
      CompileViews();
      BuildSequences();
      _driver.OnDbModelConstructed(_dbModel);
      CheckErrors();
      return _dbModel;
    }//method

    private void CheckErrors() {
      _log.CheckErrors("DbModel construction failed.");
    }

    private void LogError(string message, params object[] args) {
      _log.LogError(message, args);
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
      if (!_dbModel.Driver.Supports(DbFeatures.Views))
        return;
      var views = _dbModel.Tables.Where(t => t.Kind == EntityKind.View).ToList();
      if(views.Count == 0)
        return;
      // create dummy session, needed for LINQ translation 
      var dummySession = new EntitySession(new OperationContext(this._entityModel.App), EntitySessionKind.Dummy);
      var engine = new LinqEngine(_dbModel);
      var emptyList = new List<string>(); 
      foreach(var viewTbl in views) {
        var entInfo = viewTbl.Entity;
        var expr = entInfo.ViewDefinition.Query.Expression;
        var viewCmd = new DynamicLinqCommand(dummySession, expr, LinqCommandKind.View, LinqOperation.Select);
        LinqCommandRewriter.RewriteToLambda(viewCmd);
        var sql = engine.TranslateSelect(viewCmd);
        var cmdBuilder = new DataCommandBuilder(_driver, mode: SqlGenMode.NoParameters);
        //there might be some local values that are transformed into params. But they will be replaced with literals 
        // when generating final SQL
        cmdBuilder.AddLinqStatement(sql, viewCmd.ParamValues); 
        viewTbl.ViewSql = cmdBuilder.GetSqlText();   
      }
    }
    private static object[] _emptyArray = new object[] { };


    private void BuildSequences() {
      if (!_driver.Supports(DbFeatures.Sequences))
        return; 
      foreach(var m in _entityModel.App.Modules)
        foreach (var seq in m.Sequences) {
          var dbSeqInfo = new DbSequenceInfo(this._dbModel, seq);
          _dbModel.Sequences.Add(dbSeqInfo); 
        }
    }//method


    //Create tables and regular "value" columns
    private void BuildTables() {
      var supportsViews = _dbModel.Driver.Supports(DbFeatures.Views);
      foreach (var entity in _entityModel.Entities) {
        if (!IsActive(entity.Area))
          continue;
        if (entity.Kind == EntityKind.View && !supportsViews)
          continue; 
        var tableName = GetDbTableViewName(entity);
        var objType = entity.Kind == EntityKind.Table ? DbObjectType.Table : DbObjectType.View;
        var schema = _dbModelConfig.GetSchema(entity.Area);
        var table = new DbTableInfo(_dbModel, schema, tableName, entity, objType);
        // Check materialized view - automatically set the flag if there are indexes on the view
        if (entity.Kind == EntityKind.View) {
          if (_driver.Supports(DbFeatures.MaterializedViews) && entity.ViewDefinition.Options.IsSet(DbViewOptions.Materialized))
            table.IsMaterializedView = true;
        }
        //create Value columns 
        foreach (var member in entity.Members)
          if (member.Kind == EntityMemberKind.Column)
            CreateDbColumn(table, member);
        //reorder DbColumns, make PK appear first
        var pkColumns = table.Columns.Where(c => c.Member.Flags.IsSet(EntityMemberFlags.PrimaryKey)).ToList();
        foreach (var pkCol in pkColumns) {
          table.Columns.Remove(pkCol);
          table.Columns.Insert(0, pkCol);
        }
        //order by
        if(entity.DefaultOrderBy != null)
          table.DefaultOrderBy = ConstructDefaultOrderBy(table);
      }//foreach entityInfo
      CheckErrors();
    }

    private void CompleteTablesSetup() {
      foreach(var table in _dbModel.Tables) {
        // special columns, column lists and SQL fragments
        table.InsertColumns = table.Columns.Where(c => !c.Flags.IsSet(DbColumnFlags.NoInsert)).ToArray();
        table.UpdatableColumns = table.Columns.Where(c => !c.Flags.IsSet(DbColumnFlags.NoUpdate)).ToArray();
      }
    }

    private DbColumnInfo CreateDbColumn(DbTableInfo table, EntityMemberInfo member) {
      var colName = member.ColumnName;
      string colDefault = member.ColumnDefault; //comes from attribute
      if(colDefault != null && member.DataType == typeof(string) && !colDefault.StartsWith("'"))
        colDefault = colDefault.Quote();

      var dbTypeInfo = GetDBTypeMapping(member);
      if(dbTypeInfo == null)
        return null;
      var dbColumn = new DbColumnInfo(member, table, colName, dbTypeInfo);
      dbColumn.Converter = _driver.TypeRegistry.GetDbValueConverter(dbTypeInfo, member); 
      if (dbColumn.Converter == null) {
        LogError($"Member {member}, type {member.DataType}: failed to find DbConverter to db type {dbTypeInfo.DbTypeSpec}");
        return null; 
      }
      if(!string.IsNullOrEmpty(colDefault))
        dbColumn.DefaultExpression = colDefault;
      if(member.AutoValueType == AutoType.Identity)
        dbColumn.Flags |= DbColumnFlags.Identity | DbColumnFlags.NoUpdate | DbColumnFlags.NoInsert;
      if(member.Flags.IsSet(EntityMemberFlags.RowVersion))
        dbColumn.Flags |= DbColumnFlags.RowVersion | DbColumnFlags.NoUpdate | DbColumnFlags.NoInsert;
      if (member.Flags.IsSet(EntityMemberFlags.Secret))
        dbColumn.Flags |= DbColumnFlags.NoUpdate; //updated only thru custom update method
      if (member.Flags.IsSet(EntityMemberFlags.NoDbInsert))
        dbColumn.Flags |= DbColumnFlags.NoInsert;
      if(member.Flags.IsSet(EntityMemberFlags.NoDbUpdate))
        dbColumn.Flags |= DbColumnFlags.NoUpdate;
      return dbColumn;
    }//method

    private DbTypeInfo GetDBTypeMapping(EntityMemberInfo member) {
      DbTypeInfo typeInfo; 
      try {
        if(string.IsNullOrEmpty(member.ExplicitDbTypeSpec))
          typeInfo = _driver.TypeRegistry.GetDbTypeInfo(member);
        else
          typeInfo = _driver.TypeRegistry.GetDbTypeInfo(member.ExplicitDbTypeSpec, member);
        if (typeInfo == null)
          _log.LogError($"Failed to map member type {member.DataType} to DB type; member {member.FullName}");
        return typeInfo; 
      } catch (Exception ex) {
        _log.LogError($"Failed to map member type {member.DataType} to DB type; member {member.FullName} -  error: {ex.Message}");
        return null; 
      }
    }

    private void CreateTableKeys() {
      var supportsClustedIndex = _driver.Supports(DbFeatures.ClusteredIndexes);
      var supportsIndexedViews = _driver.Supports(DbFeatures.MaterializedViews);

      foreach (var table in _dbModel.Tables) {
        if (table.Kind == EntityKind.View && !supportsIndexedViews)
          continue; 
        var entity = table.Entity;
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
          var dbKey = new DbKeyInfo(entityKey.Name, table, keyType, entityKey);
          dbKey.KeyColumns.AddRange(keyCols);
          dbKey.IncludeColumns.AddRange(inclCols);
          //check filter
          if(entityKey.IndexFilter != null)
            dbKey.Filter = this._dbModel.Driver.SqlDialect.BuildDbTableFilter(table, entityKey.IndexFilter);
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

        //Create indexes on foreign keys - only if there's a list on the target entity 
        //    ex: index book.Publisher only if there's publisher.Books property 
        var createIndexesOnForeignKeys = !_driver.Supports(DbFeatures.ForeignKeysAutoIndexed)
                                         && _dbModelConfig.Options.IsSet(DbOptions.AutoIndexForeignKeys);
        if(createIndexesOnForeignKeys) {
          //copy FKeys into a separate list, to avoid trouble when adding indexes to collection that we are iterating
          var foreignKeys = table.Keys.Where(k => k.KeyType.IsSet(KeyType.ForeignKey)).ToList();
          foreach (var key in foreignKeys) {
            if(!ListExistsOnTargetEntity(key.EntityKey.OwnerMember))
              continue; 
            //Check if there is already index with the same starting columns
            var matchingIndex = FindMatchingIndexForForeignKey(key);
            if (matchingIndex != null)
              continue;
            var indexName = "IX_" + key.Name;
            var dbIndex = new DbKeyInfo(indexName, table, KeyType.Index);
            dbIndex.KeyColumns.AddRange(key.KeyColumns);
          }
        }//if createIndexes

      }//foreach table
      CheckErrors(); 
    }

    private bool ListExistsOnTargetEntity(EntityMemberInfo refMember) {
      Util.Check(refMember.Kind == EntityMemberKind.EntityRef, "Expected ref member.");
      var ent = refMember.Entity; 
      var targetEnt = refMember.ReferenceInfo.ToKey.Entity;
      return targetEnt.Members.Any(m => m.Kind == EntityMemberKind.EntityList && m.ChildListInfo.TargetEntity == ent);
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
          if (member.Kind != EntityMemberKind.EntityRef)
            continue;
          var refInfo = member.ReferenceInfo;
          if (!IsActive(refInfo.ToKey.Entity.Area)) 
            continue; //target entity/table is not in this db model; so no foreign key link 
          var fromDbKey = _dbModel.LookupDbObject<DbKeyInfo>(refInfo.FromKey);
          var toDbKey = _dbModel.LookupDbObject<DbKeyInfo>(refInfo.ToKey);
          var cascadeDelete = member.Flags.IsSet(EntityMemberFlags.CascadeDelete);
          var dbRC = new DbRefConstraintInfo(_dbModel, fromDbKey, toDbKey, cascadeDelete, refInfo);
        }
      }//foreach entityInfo
      CheckErrors();
    }

    private IList<DbKeyColumnInfo> ConstructDefaultOrderBy(DbTableInfo table) {
      var keyMembers = table.Entity.DefaultOrderBy;
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


    private void FinalizeObjectNames() {
      foreach (var table in _dbModel.Tables) {
        _namingPolicy?.CheckName(table);
        table.FullName = _dbModel.FormatFullName(table.Schema, table.TableName);
        table.SqlFullName = new TextSqlFragment(table.FullName);
        // columns - only for tables, does not work for views, complications with view->SQL translation
        if(table.Kind == EntityKind.Table)
          foreach(var col in table.Columns) {
            _namingPolicy?.CheckName(col);
            col.ColumnNameQuoted = _driver.SqlDialect.QuoteName(col.ColumnName);
            col.SqlColumnNameQuoted = new TextSqlFragment(col.ColumnNameQuoted);
          }
        // keys - we construct keynames after table/column names were finalized
        foreach(var key in table.Keys) {
          CheckAssignDbKeyName(key);
          CheckDbKeyNameUnique(key);
          _namingPolicy?.CheckName(key); 
        }
      } //foreach
      _dbModel.RefreshTablesByName(); 
    }//method


    public void CheckAssignDbKeyName(DbKeyInfo dbKey) {
      // note: some dbKeys do not have entity key (ex: indexes on foreign keys)
      if (!string.IsNullOrEmpty(dbKey.Name))
        return; 
      if (dbKey.EntityKey?.Name != null) {
        dbKey.Name = dbKey.EntityKey.Name;
        return; 
      }
      // construct the name
      var tbl = dbKey.Table;
      var prefix = Entities.Model.Construction.EntityModelBuilderHelper.GetKeyNamePrefix(dbKey.KeyType);
      if(dbKey.KeyType.IsSet(KeyType.PrimaryKey)) {
        dbKey.Name = prefix + tbl.TableName; // PK_Book
      } else if(dbKey.KeyType.IsSet(KeyType.ForeignKey)) {
        // -> FK_Book_Publisher
        var fk = dbKey.Table.RefConstraints.FirstOrDefault(c => c.FromKey == dbKey);
        Util.Check(fk != null, "Fatal: failed to find DbRefConstraint for key type {0}, table: {1}", dbKey.KeyType, tbl.TableName);
        var targetTable = fk.ToKey.Table;
        dbKey.Name = prefix + tbl.TableName + "_" + targetTable.TableName; 
      } else {
        // IXC_Book_CreatedOnId
        var keyCols = dbKey.KeyColumns.GetNames(removeUnderscores: true);
        dbKey.Name = prefix + tbl.TableName + "_" + keyCols;
      }
    }

    private void CheckDbKeyNameUnique(DbKeyInfo dbKey) {
      var maxLen = _dbModel.Driver.MaxKeyNameLength; 
      if(dbKey.Name.Length > maxLen) //protect against too long names
        dbKey.Name = dbKey.Name.Substring(0, maxLen - 1);
      var cnt = 0;
      var baseName = dbKey.Name;
      var allKeys = dbKey.Table.Keys;
      while(true) {
        // if no dupes, then return
        if(!allKeys.Any(k => k != dbKey && dbKey.Name.Equals(k.Name, StringComparison.OrdinalIgnoreCase)))
          return;
        dbKey.Name = baseName + cnt++;
      }
    }

    public virtual string GetDbTableViewName(EntityInfo entity) {
      // if table name was set explicitly in entity model (using attr), then use it
      var tname = entity.TableName; 
      if(!string.IsNullOrWhiteSpace(tname))
        return tname;
      switch(entity.Kind) {
        case EntityKind.View:
          tname = _dbModelConfig.DbViewPrefix + entity.Name;
          break;
        case EntityKind.Table:
        default:
          tname = entity.Name;
          break;
      }
      if(_dbModelConfig.Options.IsSet(DbOptions.PluralizeTableNames))
        tname = StringHelper.Pluralize(tname);
      if(_dbModelConfig.Options.IsSet(DbOptions.AddSchemaToTableNames))
        tname = entity.Area.Name + "_" + tname;
      return tname;
    }//method


  }//class
}//namespace
