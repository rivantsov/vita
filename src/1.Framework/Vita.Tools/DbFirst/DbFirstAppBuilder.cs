using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Utilities;
using Vita.Entities.Logging;
using Vita.Data.Driver.TypeSystem;

namespace Vita.Tools.DbFirst {

  /// <summary>Reconstructs the entity model from the raw DbModel loaded from the database. 
  /// The constructed entity model may be used for generating the entity interfaces as c# source file. </summary>
  public partial class DbFirstAppBuilder {
    /*
    private Type CreateDummyEntityType(string entityTypeName) {
      var fullName = _tempNamespace + "." + entityTypeName;
      var typeAttrs = TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract;
      var typeBuilder = _entityModel.ClassesAssembly.ModuleBuilder.DefineType(fullName, typeAttrs);
      return typeBuilder.CreateTypeInfo().AsType();
    }
    */

    IProcessFeedback _feedback;
    DbFirstConfig _config; 
    DbSettings _dbSettings;
    DbModel _dbModel;
    EntityApp _app;
    EntityModel _entityModel;
    IDbTypeRegistry _typeRegistry; 

    public DbFirstAppBuilder(IProcessFeedback feedback) {
      _feedback = feedback; 
    }

    public EntityApp Build(DbFirstConfig config) {
      _config = config;
      _app = new EntityApp();
      _entityModel = new EntityModel(_app);
      _dbSettings = new DbSettings(_config.Driver, config.Driver.GetDefaultOptions(), _config.ConnectionString);
      // create loader and load model
      var log = new BufferedLog();
      var modelLoader = _config.Driver.CreateDbModelLoader(_dbSettings, log);
      modelLoader.SetSchemasSubset(_config.Schemas); 
      _dbModel = modelLoader.LoadModel();
      _typeRegistry = _dbModel.Driver.TypeRegistry;
      Util.Check(_dbModel.Tables.Count() > 0, "No tables found in the database. Code generation aborted.");
      // Generate entity model
      GenerateModulesAndAreas(); 
      GenerateEntities();
      GenerateKeys(); 
      GenerateReferenceMembers();
      AssignOwnerMembersToKeysMatchingForeignKeys();
      SetupNonExpandedKeyMembers();
      GenerateListMembers();

      return _app; 
    }

    private void GenerateModulesAndAreas() {
      if( _dbModel.Driver.Supports(DbFeatures.Schemas)) {
       // var schemas = _dbSettings.GetSchemas().ToList();
        foreach(var schInfo in _dbModel.Schemas) {
          var sch = schInfo.Schema;
         // if(schemas.Count > 0 && !schemas.Contains(sch))
         //   continue;
          var area = _app.AddArea(sch);
          var module = new EntityModule(area, "EntityModule" + sch.FirstCap());
        }
      } else {
        var area = _app.AddArea("Default");
        var module = new EntityModule(area, "EntityModuleDefault");
      }
    }

    private void GenerateEntities() {
      foreach (var table in _dbModel.Tables) {
        if(_config.IgnoreTables.Contains(table.TableName))
          continue;
        var module = GetModule(table.Schema);
        var entName = GenerateEntityName(table);
        var entType = typeof(object); // CreateDummyEntityType(entName); //dummy type, just to have unique type instance
        // it is the same typeof(object) for all entities, but the non-empty Entities list indicates that module is not empty 
        module.Entities.Add(entType); 
        // we add only entity types for tables; views are ignored (we do not have queries to create view definitions)
        //if (table.Kind == EntityKind.Table) module.Entities.Add(entType); // register type in module
        // Note: we generate entity interfaces for Views, but do not register them as entities
        var entInfo = new EntityInfo(module, entType, table.Kind);
        entInfo.SetCodeGenName(entName);
        entInfo.TableName = table.TableName;
        table.Entity = entInfo; 
        _entityModel.RegisterEntity(entInfo);
        GenerateEntityMembers(table);
      }//foreach table
    }//method

    private void GenerateEntityMembers(DbTableInfo table) {
      var entInfo = table.Entity;
      // generate entity members
      foreach (var col in table.Columns) {
        var nullable = col.Flags.IsSet(DbColumnFlags.Nullable);
        var memberDataType = GetMemberType(col);
        var memberName = CheckMemberName(DbNameToCsName(col.ColumnName), entInfo);
        var member = col.Member = new EntityMemberInfo(entInfo, EntityMemberKind.Column, memberName, memberDataType);
        member.ColumnName = col.ColumnName;
        member.IsProperty = true; 
        // member is added to entInfo.Members automatically in constructor
        if (nullable)
          member.Flags |= EntityMemberFlags.Nullable; // in case it is not set (for strings)
        if (col.Flags.IsSet(DbColumnFlags.Identity)) {
          member.Flags |= EntityMemberFlags.Identity;
          member.AutoValueType = AutoType.Identity;
        }
        //hack for MS SQL
        if (col.TypeInfo.TypeDef.Name == "timestamp")
          member.AutoValueType = AutoType.RowVersion;
        member.Size = (int)col.TypeInfo.Size;
        member.Scale = col.TypeInfo.Scale;
        member.Precision = col.TypeInfo.Precision;
        //Check if we need to specify DbType or DbType spec explicitly
        bool unlimited = member.Size < 0;
        if (unlimited)
          member.Flags |= EntityMemberFlags.UnlimitedSize;
        var typeDef = col.TypeInfo.TypeDef;

        // Detect if we need to set explicity DbType or DbTypeSpec in member attribute
        var dftMapping = _typeRegistry.GetDbTypeInfo(member);
        if (col.TypeInfo.Matches(dftMapping))
          continue; //no need for explicit DbTypeSpec
                    //DbTypeMapping is not default for this member - we need to specify DbType or TypeSpec explicitly
        member.ExplicitDbTypeSpec = col.TypeInfo.DbTypeSpec;
        if (member.Flags.IsSet(EntityMemberFlags.Identity))
          entInfo.Flags |= EntityFlags.HasIdentity;
      }
    }

    private void GenerateKeys() {
      var fkAutoIndexed = _dbModel.Driver.Supports(DbFeatures.ForeignKeysAutoIndexed);
      foreach (var table in _dbModel.Tables) {
        var ent = table.Entity; 
        if (ent == null)
          continue; //ignore this table

        foreach (var dbKey in table.Keys) {
          var keyType = dbKey.KeyType;
          if (fkAutoIndexed) {
            // MySql, FKs are autoindexed: do not put explicit index on FK in entity model; so we drop Index flag 
            var isFkIndex = dbKey.KeyType.IsSet(KeyType.ForeignKey) && dbKey.KeyType.IsSet(KeyType.Index);
            if (isFkIndex)
              keyType &= ~KeyType.Index;
          }
          var entKey = dbKey.EntityKey = new EntityKeyInfo(table.Entity, keyType); //added automatically to entity.Keys
          entKey.Name = dbKey.Name;
          foreach (var keyCol in dbKey.KeyColumns) {
            entKey.KeyMembersExpanded.Add(new EntityKeyMemberInfo(keyCol.Column.Member, keyCol.Desc));
          }
          if (dbKey.KeyType.IsSet(KeyType.Clustered))
            table.Entity.Flags |= EntityFlags.HasClusteredIndex;
          // primary key
          if (entKey.KeyType.IsSet(KeyType.PrimaryKey)) {
            ent.PrimaryKey = entKey;
            foreach (var km in entKey.KeyMembersExpanded)
              km.Member.Flags |= EntityMemberFlags.PrimaryKey;
          }

          BuildIndexFilterIncludes(dbKey);
        } //foreach key
        //check primary key
        if (ent.Kind == EntityKind.Table && ent.PrimaryKey == null) {
          _feedback.SendFeedback(FeedbackType.Warning, "WARNING: Table {0} has no primary key.", table.TableName);
        }
      }//foreach table
    }//method

    // Creates ref members and foreign keys
    private void GenerateReferenceMembers() {
      foreach (var table in _dbModel.Tables) {
        if (table.Entity == null)
          continue;
        
        var ent = table.Entity;
        foreach (var constr in table.RefConstraints) {
          var targetEnt = constr.ToKey.Table.Entity;
          var fromDbKey = constr.FromKey;
          var fromEntKey = fromDbKey.EntityKey; 
          
          string memberName = GetRefMemberName(constr);
          var refMember = new EntityMemberInfo(ent, EntityMemberKind.EntityRef, memberName, targetEnt.EntityType);
          refMember.IsProperty = true; 
          ent.RefMembers.Add(refMember);
          fromEntKey.KeyMembers.Add(new EntityKeyMemberInfo(refMember));
          fromEntKey.OwnerMember = refMember;
          refMember.ReferenceInfo = new EntityReferenceInfo(refMember, fromEntKey, targetEnt.PrimaryKey);

          if (constr.CascadeDelete)
            refMember.Flags |= EntityMemberFlags.CascadeDelete;
          // mark flags 
          foreach (var km in fromEntKey.KeyMembersExpanded) {
            km.Member.Flags |= EntityMemberFlags.ForeignKey;
            if (km.Member.Flags.IsSet(EntityMemberFlags.Nullable))
              refMember.Flags |= EntityMemberFlags.Nullable;
            // mark column as non-property since it is inside entity ref
            km.Member.IsProperty = false; 
          }
        }
      }//foreach table
    }

    private void AssignOwnerMembersToKeysMatchingForeignKeys() {
      foreach(var ent in _entityModel.Entities) {
        // for each FK, find non-FK keys with the same columns; copy OwnerMember of FK to these matching non-FK keys 
        var fks = ent.Keys.Where(k => k.KeyType.IsSet(KeyType.ForeignKey)).ToList();
        var nonFks = ent.Keys.Where(k => k.OwnerMember == null && !k.KeyType.IsSet(KeyType.ForeignKey)).ToList();
        foreach (var nonFk in nonFks) {
          foreach (var fk in fks)
            if (KeyExpandedMembersMatch(fk, nonFk)) {
              nonFk.OwnerMember = fk.OwnerMember;
              nonFk.KeyMembers.Clear();
              nonFk.KeyMembers.Add(new EntityKeyMemberInfo(fk.OwnerMember));
            }
        }
      }
    }

    private void GenerateListMembers() {
      bool addLists = _config.Options.IsSet(DbFirstOptions.AddOneToManyLists);
      if (!addLists)
        return; 
      foreach (var ent in _entityModel.Entities) 
        foreach(var refMember in ent.RefMembers) {
          var memberIndexKey = refMember.Entity.Keys.FirstOrDefault(k => k.OwnerMember == refMember && k.KeyType.IsSet(KeyType.Index));
          if (memberIndexKey == null)
            continue;
          //create List member on target entity
          var targetEnt = refMember.ReferenceInfo.ToKey.Entity;
          var listName = StringHelper.Pluralize(ent.Name.Substring(1)); //remove 'I' prefix and pluralize
          listName = CheckMemberName(listName, targetEnt);
          var listType = typeof(IList<>).MakeGenericType(ent.EntityType);
          var listMember = new EntityMemberInfo(targetEnt, EntityMemberKind.EntityList, listName, listType);
          listMember.IsProperty = true; 
          var listInfo = listMember.ChildListInfo = new ChildEntityListInfo(listMember);
          listInfo.RelationType = EntityRelationType.ManyToOne;
          listInfo.TargetEntity = ent;
          listInfo.ParentRefMember = refMember;
          refMember.ReferenceInfo.TargetListMember = listMember;
      }
    }
  


    // When we initially created entity keys, we filled out dbKey.ExplandedMembers list - these include real FK members (ex: IBook.Publisher_Id). 
    // We now need to build dbKey.Members lists that include refMembers (ex: IBook.Publisher) instead of FK members in dbKey.ExpandedMembers
    // c# entity definitions use entity reference members in keys and indexes, ex: [Index("Publisher"] on IBook interface.
    private void SetupNonExpandedKeyMembers() {
      foreach (var ent in _entityModel.Entities) {
        foreach (var key in ent.Keys) {
          // if there is a member in KeyMembers (non-expanded members), skip it, it is already dones; otherwise copy from expanded members
          if (key.KeyMembers.Count > 0)
            continue; 
          key.KeyMembers.AddRange(key.KeyMembersExpanded);
          // if it is a single column and it is 'visible', set it as key owner; ex: column Id which is PK (single column), 
          // we want PrimaryKey to appear on this Id property, not on entity with list of columns
          if (key.KeyMembersExpanded.Count == 1) {
            var km0 = key.KeyMembersExpanded[0];
            key.KeyMembers.Add(km0);
            if (km0.Member.IsProperty)
              key.OwnerMember = km0.Member;
          }
        }//foreach dbKey
      }//foreach entInfo            
    }//method


    //We have one-module per area, so we find module by associated area's schema 
    private EntityModule GetModule(string schema) {
      //Account for providers that do not support schemas (SQL CE, SQLite)
      if(string.IsNullOrEmpty(schema) && !_dbSettings.ModelConfig.Driver.Supports(DbFeatures.Schemas))
        return _app.Modules.FirstOrDefault(); 
      var module = _app.Modules.FirstOrDefault(m => m.Area.Name.Equals(schema, StringComparison.OrdinalIgnoreCase));
      Util.Check(module != null, "EntityModule for schema {0} not found.", schema);
      return module;
    }

  }//class
}//ns
