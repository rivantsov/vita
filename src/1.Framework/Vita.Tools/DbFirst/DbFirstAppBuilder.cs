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
      AssignPrimaryKeys();
      GenerateReferenceMembers();
      GenerateListMembers();

      SetupNonExpandedKeyMembers();
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

    private void GenerateKeys() {
      var fkAutoIndexed = _dbModel.Driver.Supports(DbFeatures.ForeignKeysAutoIndexed);
      foreach (var table in _dbModel.Tables) {
        if (table.Entity == null)
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

          BuildIndexFilterIncludes(dbKey);
        }
      }//foreach table
    }//method

    private void AssignPrimaryKeys() {
      foreach (var table in _dbModel.Tables) {
        var ent = table.Entity;
        if (ent == null || table.Kind == EntityKind.View)
          continue; //ignore this table
        ent.PrimaryKey = ent.Keys.FirstOrDefault(k => k.KeyType.IsSet(KeyType.PrimaryKey));
        if (ent.PrimaryKey == null) {
          _feedback.SendFeedback(FeedbackType.Warning, "WARNING: Table {0} has no primary key.", table.TableName);
          //create fake
          //ent.PrimaryKey = new EntityKeyInfo(table.Entity, KeyType.PrimaryKey);
        }
        ent.PrimaryKey.KeyMembers.Each(km => km.Member.Flags |= EntityMemberFlags.PrimaryKey);
      } // foreach 
    }

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
          ent.RefMembers.Add(refMember);
          fromEntKey.KeyMembers.Add(new EntityKeyMemberInfo(refMember));

          if (constr.CascadeDelete)
            refMember.Flags |= EntityMemberFlags.CascadeDelete;
          // mark flags 
          foreach (var km in fromDbKey.EntityKey.KeyMembersExpanded) {
            km.Member.Flags |= EntityMemberFlags.ForeignKey;
            if (km.Member.Flags.IsSet(EntityMemberFlags.Nullable))
              refMember.Flags |= EntityMemberFlags.Nullable;
          }
          refMember.ReferenceInfo = new EntityReferenceInfo(refMember, fromEntKey, targetEnt.PrimaryKey);
          // assign this ref member as owner to fk and indexes with matching columns
          AssignOwnerMemberToMatchingKeys(refMember, fromDbKey);
        }
      }//foreach table
    }

    private void AssignOwnerMemberToMatchingKeys(EntityMemberInfo refMember, DbKeyInfo fkDbKey) {
      var allTableKeys = fkDbKey.Table.Keys;
      foreach (var dbkey in allTableKeys) {
        if (!ColumnsMatch(fkDbKey, dbkey))
          continue;
        // Set owner and add it to key members
        var entKey = dbkey.EntityKey;
        entKey.OwnerMember = refMember;
        entKey.KeyMembers.Clear();
        entKey.KeyMembers.Add(new EntityKeyMemberInfo(refMember));
      }
    }

    private bool ColumnsMatch(DbKeyInfo key1, DbKeyInfo key2) {
      if (key1.KeyColumns.Count != key2.KeyColumns.Count)
        return false;
      for (int i = 0; i < key1.KeyColumns.Count; i++)
        if (key1.KeyColumns[i].Column != key2.KeyColumns[i].Column)
          return false;
      return true;
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
          // if there is a member, it is entity ref member; otherwise just copy from expanded
          if (key.KeyMembers.Count == 0)
            key.KeyMembers.AddRange(key.KeyMembersExpanded);
        }//foreach dbKey
      }//foreach entInfo            
    }//method


    private string CheckMemberName(string baseName, EntityInfo entity, string trySuffix = null) {
      var name = baseName; 
      if (entity.GetMember(name) == null)
        return name;
      // For entity references we might have occasional match of baseName with the FK column name. To avoid adding numbers, we try to add "Ref" at the end.
      if (!string.IsNullOrEmpty(trySuffix)) {
        name = baseName + trySuffix;
        if (entity.GetMember(name) == null)
          return name; 
      }
      // try adding number at the end.
      for (int i = 1; i < 10; i++) {
        name = baseName + i;
        if (entity.GetMember(name) == null)
          return name; 
      }
      Util.Throw("Failed to generate property name for entity {0}, base name {1}", entity.Name, baseName);
      return null; 
    }

    //We have one-module per area, so we find module by associated area's schema 
    private EntityModule GetModule(string schema) {
      //Account for providers that do not support schemas (SQL CE, SQLite)
      if(string.IsNullOrEmpty(schema) && !_dbSettings.ModelConfig.Driver.Supports(DbFeatures.Schemas))
        return _app.Modules.FirstOrDefault(); 
      var module = _app.Modules.FirstOrDefault(m => m.Area.Name.Equals(schema, StringComparison.OrdinalIgnoreCase));
      Util.Check(module != null, "EntityModule for schema {0} not found.", schema);
      return module;
    }

    private Type GetMemberType(DbColumnInfo colInfo) {
      var nullable = colInfo.Flags.IsSet(DbColumnFlags.Nullable);
      var typeInfo = colInfo.TypeInfo;
      var mType = typeInfo.ClrType;
      if(mType.IsValueType && nullable)
        mType = ReflectionHelper.GetNullable(mType);
      Type forcedType;
      if(_config.ForceDataTypes.TryGetValue(colInfo.ColumnName, out forcedType))
        return forcedType;
      if(mType == typeof(byte[])) {
        var changeToGuid =
          _config.Options.IsSet(DbFirstOptions.Binary16AsGuid) && typeInfo.Size == 16 ||
          _config.Options.IsSet(DbFirstOptions.BinaryKeysAsGuid) && colInfo.Flags.IsSet(DbColumnFlags.PrimaryKey | DbColumnFlags.ForeignKey);
        if(changeToGuid)
          return typeof(Guid); 
      }
      return mType;
    }

    // removes spaces and underscores, changes to sentence case
    private static string DbNameToCsName(string name) {
      if (name == null) return null;
      //Do uppercasing first
      string nameUpper = name; 
      if (name.IndexOf('_') == -1)
        nameUpper = name.FirstCap();
      else {
        //Convert to first cap all segments separated by underscore
        var parts = name.Split(new [] {'_', ' '}, StringSplitOptions.RemoveEmptyEntries);
        nameUpper = String.Join(string.Empty, parts.Select(p => p.FirstCap()));
      }
      //Check all chars they are CLR compatible
      var chars = nameUpper.ToCharArray(); 
      for(int i = 0; i < chars.Length; i++) {
        if(char.IsLetterOrDigit(chars[i]) || chars[i] == '_')
          continue; 
        chars[i] = 'X';
      }
      nameUpper = new string(chars); 
      return nameUpper;
    }



  }//class
}//ns
