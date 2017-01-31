using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Data;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Model.Construction;
using Vita.Data;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Runtime;
using System.Xml;

namespace Vita.Tools.DbFirst {

  /// <summary>Reconstructs the entity model from the raw DbModel loaded from the database. 
  /// The constructed entity model may be used for generating the entity interfaces as c# source file. </summary>
  public class DbFirstAppBuilder {
    IProcessFeedback _feedback;
    DbFirstConfig _config; 
    DbSettings _dbSettings;
    DbModel _dbModel;
    EntityApp _app; 
    EntityModel _entityModel;
    DbTypeRegistry _typeRegistry;

    static int _callCount; // artificial counter to generate unique temp namespaces for multiple calls
    string _tempNamespace; //temp namespace for generating interface Types; 

    public DbFirstAppBuilder(IProcessFeedback feedback) {
      _feedback = feedback; 
    }


    public EntityApp Build(DbFirstConfig config) {
      _config = config;
      _app = new EntityApp();
      var log = _app.ActivationLog;
      _dbSettings = new DbSettings(_config.Driver, DbOptions.Default, _config.ConnectionString);
      _dbSettings.SetSchemas(_config.Schemas);
      var modelLoader = _config.Driver.CreateDbModelLoader(_dbSettings, log);
      _dbModel = modelLoader.LoadModel();
      Util.Check(_dbModel.Tables.Count() > 0, "No tables found in the database. Code generation aborted.");
      // Prepare type generator
      _tempNamespace = "_dummy_" + _callCount++; // artificial unique namespace for dummy interface types
      // Construct model setup and model
      GenerateModulesAndAreas(); 
      _entityModel = new EntityModel(_app);
      EntityModelBuilder.SetModel(_app, _entityModel); 
      _entityModel.ClassesAssembly = new EntityClassesAssembly(); 
      //generate entities and members
      GenerateEntities();
      SetupPrimaryKeys();
      GenerateReferenceMembers();
      CreateIndexes();
      SetupKeyMembers();
      return _app; 
    }


    private void GenerateModulesAndAreas() {
      if( _dbModel.Driver.Supports(DbFeatures.Schemas)) {
        var schemas = _dbSettings.GetSchemas().ToList();
        foreach(var schInfo in _dbModel.Schemas) {
          var sch = schInfo.Schema;
          if(schemas.Count > 0 && !schemas.Contains(sch))
            continue;
          var area = _app.AddArea(sch);
          var module = new EntityModule(area, "EntityModule" + sch.FirstCap());
        }
      } else {
        var area = _app.AddArea("Default");
        var module = new EntityModule(area, "EntityModuleDefault");
      }
    }


    private void GenerateEntities() {
      _typeRegistry = _dbModel.Driver.TypeRegistry;
      var viewPrefix = _dbModel.Config.NamingPolicy.ViewPrefix;
      var tablePrefix = _dbModel.Config.NamingPolicy.TablePrefix;
      //track uniqueness of type names - we might have trouble if we have 2 tables with the same name in different schemas 
      var typeNames = new StringSet();
      foreach (var table in _dbModel.Tables) {
        if(_config.IgnoreTables.Contains(table.TableName))
          continue;
        var module = GetModule(table.Schema);
        var entName = DbNameToCsName(table.TableName);
        switch(table.Kind) {
          case EntityKind.Table:
            if (string.IsNullOrWhiteSpace(tablePrefix) && entName.StartsWith(tablePrefix))
              entName = entName.Substring(tablePrefix.Length);
            break; 
          case EntityKind.View:
            if (!string.IsNullOrWhiteSpace(viewPrefix) && entName.StartsWith(viewPrefix))
              entName = entName.Substring(viewPrefix.Length);
            break; 
        }
        if (_config.Options.IsSet(DbFirstOptions.ChangeEntityNamesToSingular))
          entName = StringHelper.Unpluralize(entName); 
        entName = "I" + entName;
        if(typeNames.Contains(entName))
          entName = entName + "_" + table.Schema; 
        var entType = CreateDummyEntityType(entName); //dummy type, just to have unique type instance
        typeNames.Add(entName);
        // we add only entity types for tables; views are ignored (we do not have queires to create view definitions)
        if (table.Kind == EntityKind.Table)
          module.Entities.Add(entType); // register type in module
        // Note: we generate entity interfaces for Views, but do not register them as entities
        var ent = new EntityInfo(module, entType, table.Kind);
        ent.TableName = table.TableName;
        table.Entity = ent; 
        _entityModel.RegisterEntity(ent); 
        // generate entity members
        foreach (var col in table.Columns) {
          var nullable = col.Flags.IsSet(DbColumnFlags.Nullable);
          var memberDataType = GetMemberType(col);
          var memberName = CheckMemberName(DbNameToCsName(col.ColumnName), ent);
          var member = col.Member = new EntityMemberInfo(ent, MemberKind.Column, memberName, memberDataType);
          member.ColumnName = col.ColumnName;
          // member is added to ent.Members automatically in constructor
          if (nullable)
            member.Flags |= EntityMemberFlags.Nullable; // in case it is not set (for strings)
          if(col.Flags.IsSet(DbColumnFlags.Identity)) {
            member.Flags |= EntityMemberFlags.Identity;
            member.AutoValueType = AutoType.Identity;
          }
          //hack
          if (col.TypeInfo.VendorDbType.TypeName == "timestamp")
            member.AutoValueType = AutoType.RowVersion;
          member.Size = (int)col.TypeInfo.Size;
          member.Scale = col.TypeInfo.Scale;
          member.Precision = col.TypeInfo.Precision;
          //Check if we need to specify DbType or DbType spec explicitly
          bool isMemo = member.Size < 0;
          if(isMemo)
            member.Flags |= EntityMemberFlags.UnlimitedSize;
          var typeDef = col.TypeInfo.VendorDbType;

          var dftTypeDef = _typeRegistry.FindVendorDbTypeInfo(member.DataType, isMemo);
          if(typeDef == dftTypeDef)
            continue; //no need for explicit DbType
          /*
          bool typeIsDefault =  typeDef.ColumnOutType == dataType && typeDef.Flags.IsSet(VendorDbTypeFlags.IsDefaultForClrType);
          if (typeIsDefault)
            continue; //no need for explicit DbType
          */
          //DbTypeDef is not default for this member - we need to specify DbType or TypeSpec explicitly
          // Let's see if explicit DbType is enough; let's try to search by DbType and check if it brings the same db type
          var vendorTypeDef = _typeRegistry.FindVendorDbTypeInfo(col.TypeInfo.VendorDbType.DbType, memberDataType, isMemo);
          if (vendorTypeDef == typeDef)
            member.ExplicitDbType = col.TypeInfo.DbType; //Explicit db type is enough
          else
            member.ExplicitDbTypeSpec = col.TypeInfo.SqlTypeSpec;
        }
      }//foreach table
    }//method

    private void SetupPrimaryKeys() {
      foreach (var table in _dbModel.Tables) {
        if(table.Entity == null || table.Kind == EntityKind.View)
          continue; //ignore this table
        var ent = table.Entity;
        ent.PrimaryKey = new EntityKeyInfo("PK_" + ent.Name, KeyType.PrimaryKey, ent);
        if (table.PrimaryKey == null) {
          _feedback.SendFeedback(FeedbackType.Warning, "WARNING: Table {0} has no primary key.", table.TableName);
          table.PrimaryKey = new DbKeyInfo("PK_" + table.TableName + "_EMPTY", table, KeyType.PrimaryKey);
          continue; // next table
        }
        if(table.PrimaryKey.KeyType.IsSet(KeyType.Clustered))
          ent.PrimaryKey.KeyType |= KeyType.Clustered;
        foreach (var keyCol in table.PrimaryKey.KeyColumns) {
          keyCol.Column.Member.Flags |= EntityMemberFlags.PrimaryKey;
          ent.PrimaryKey.ExpandedKeyMembers.Add(new EntityKeyMemberInfo(keyCol.Column.Member, keyCol.Desc));
        }
      }
    }

    private void CreateIndexes() {
      var fkAutoIndexed = !_dbModel.Driver.Supports(DbFeatures.NoIndexOnForeignKeys);
      var supportsIncludeOrFilter = _dbModel.Driver.Supports(DbFeatures.IncludeColumnsInIndexes | DbFeatures.FilterInIndexes);
      foreach (var table in _dbModel.Tables) {
        if(table.Entity == null)
          continue; //ignore this table
        bool hasClusteredIndex = false; 
        foreach (var key in table.Keys) {
          if (key.KeyType.IsSet(KeyType.Clustered))
            hasClusteredIndex = true;
          if (!key.KeyType.IsSet(KeyType.Index)) 
            continue;
          if (fkAutoIndexed && key.KeyType.IsSet(KeyType.ForeignKey)) {
            // NOTE: commented out - do not put explicit index on FK in entity model; it is added only on DbModel!
            //if (key.EntityKey != null)  key.EntityKey.KeyType |= KeyType.Index;
            continue;
          }
          var entKey = key.EntityKey = new EntityKeyInfo(key.Name, key.KeyType, table.Entity); //added automatically to entity.Keys
          foreach (var keyCol in key.KeyColumns)
            entKey.ExpandedKeyMembers.Add(new EntityKeyMemberInfo(keyCol.Column.Member, keyCol.Desc));
          if (entKey.KeyType.IsSet(KeyType.Index) && supportsIncludeOrFilter) {
            entKey.Filter = key.Filter;
            //carefully add included members
            var incMembers = key.IncludeColumns.Select(c => c.Member).ToList();
            foreach(var m in incMembers) {
              if(m.Flags.IsSet(EntityMemberFlags.ForeignKey))
                entKey.IncludeMembers.Add(m.ForeignKeyOwner);
              else
                entKey.IncludeMembers.Add(m); 
            }
          }
        }
        if (hasClusteredIndex)
          table.Entity.Flags |= EntityFlags.HasClusteredIndex;
      }//foreach table
    }//method

    // When we initially created entity keys, we filled out key.ExplandedMembers list - these include real FK members (ex: IBook.Publisher_Id). 
    // We now need to build key.Members lists that include refMembers (ex: IBook.Publisher) instead of FK members in key.ExpandedMembers
    // c# entity definitions use entity reference members in keys and indexes, ex: [Index("Publisher"] on IBook interface.
    private void SetupKeyMembers() {
      foreach (var ent in _entityModel.Entities) {
        foreach (var key in ent.Keys) {
          foreach (var keyMember in key.ExpandedKeyMembers) {
            var member = keyMember.Member; 
            if (member.Flags.IsSet(EntityMemberFlags.ForeignKey)) {
              // It is foreign key member; replace it with refMember 
              var refMember = member.ForeignKeyOwner;
              var allFkKeyMembers = member.ForeignKeyOwner.ReferenceInfo.FromKey.ExpandedKeyMembers;
              bool canUseRefMember = ContainsAll(key.ExpandedKeyMembers, allFkKeyMembers);
              if (canUseRefMember) {
                // add it if it is not there yet. Note we might have multiple FK members for a single ref member (if FK is composite)
                if (!key.KeyMembers.Any(km => km.Member == refMember)) 
                  key.KeyMembers.Add(new EntityKeyMemberInfo(refMember, false));
              } else //add member itself
                key.KeyMembers.Add(keyMember); 
            } else //if is FK
              // it is regular member, not FK. Add it to Members list
              key.KeyMembers.Add(keyMember); 
            
          }//foreach member
        }//foreach key
      }//foreach ent            
    }//method

    private bool ContainsAll(IList<EntityKeyMemberInfo> list, IList<EntityKeyMemberInfo> subList) {
      foreach(var skm in subList)
        if(!list.Any(km => km.Member == skm.Member))
          return false;
      return true;
    }

    // Creates ref members and foreign keys
    private void GenerateReferenceMembers() {
      bool addListMembers = _config.Options.IsSet(DbFirstOptions.AddOneToManyLists);
      foreach (var table in _dbModel.Tables) {
        if(table.Entity == null)
          continue; 
        var ent = table.Entity; 
        foreach(var constr in table.RefConstraints) {
          //create ref member
          var targetEnt = constr.ToKey.Table.Entity;
          string memberName;
          if (constr.FromKey.KeyColumns.Count == 1) {
            var fkColName = constr.FromKey.KeyColumns[0].Column.ColumnName;
            memberName = fkColName;
            // cut-off PK name; but first check what is PK column; if it is just Id, then just cut it off. But sometimes PK includes entity name, like book_id; 
            // In this case, cut-off only '_id'
            var cutOffSuffix = constr.ToKey.KeyColumns[0].Column.ColumnName; //book_id?
            var targetTableName = constr.ToKey.Table.TableName;
            if (cutOffSuffix.StartsWith(targetTableName))
              cutOffSuffix = cutOffSuffix.Substring(targetTableName.Length).Replace("_", string.Empty); // "Id"
            if (memberName.EndsWith(cutOffSuffix, StringComparison.OrdinalIgnoreCase)) {
              memberName = memberName.Substring(0, memberName.Length - cutOffSuffix.Length);
              if (string.IsNullOrEmpty(memberName))
                memberName = targetEnt.Name;
            }
            if (memberName == fkColName) //that is a problem - we cannot have dup names; change to target entity name
              memberName = targetEnt.Name;
          } else 
            memberName = targetEnt.Name; //without starting "I"
          memberName = DbNameToCsName(memberName);
          memberName = CheckMemberName(memberName, ent, trySuffix: "Ref"); 
          var refMember = new EntityMemberInfo(ent, MemberKind.EntityRef, memberName, targetEnt.EntityType);
          if (constr.CascadeDelete)
            refMember.Flags |= EntityMemberFlags.CascadeDelete;
          // create foreign key
          var toDbKey = constr.FromKey;
          var toEntKey = toDbKey.EntityKey = new EntityKeyInfo(toDbKey.Name, KeyType.ForeignKey, ent, refMember); // added to ent.Keys automatically
          foreach(var toKeyCol in toDbKey.KeyColumns) {
            toEntKey.ExpandedKeyMembers.Add(new EntityKeyMemberInfo(toKeyCol.Column.Member, toKeyCol.Desc)); 
            toKeyCol.Column.Member.Flags |= EntityMemberFlags.ForeignKey;
            toKeyCol.Column.Member.ForeignKeyOwner = refMember;
            if (toKeyCol.Column.Member.Flags.IsSet(EntityMemberFlags.Nullable))
              refMember.Flags |= EntityMemberFlags.Nullable; 
          }
          refMember.ReferenceInfo = new EntityReferenceInfo(refMember, toEntKey, targetEnt.PrimaryKey);
          if (addListMembers) {
            //create List member on target entity
            var listName = StringHelper.Pluralize(ent.EntityType.Name.Substring(1)); //remove 'I' prefix and pluralize
            listName = CheckMemberName(listName, targetEnt);
            var listType = typeof(IList<>).MakeGenericType(ent.EntityType);
            var listMember = new EntityMemberInfo(targetEnt, MemberKind.EntityList, listName, listType);
            var listInfo = listMember.ChildListInfo = new ChildEntityListInfo(listMember);
            listInfo.RelationType = EntityRelationType.ManyToOne;
            listInfo.TargetEntity = ent;
            listInfo.ParentRefMember = refMember;
            refMember.ReferenceInfo.TargetListMember = listMember; 
          }
        }
      }//foreach table
    }

    private string CheckMemberName(string baseName, EntityInfo entity, string trySuffix = null) {
      var result = baseName; 
      if (entity.GetMember(result) == null)
        return result;
      // For entity references we might have occasional match of baseName with the FK column name. To avoid adding numbers, we try to add "Ref" at the end.
      if (!string.IsNullOrEmpty(trySuffix)) {
        result = baseName + trySuffix;
        if (entity.GetMember(result) == null)
          return result; 
      }
      // try adding number at the end.
      for (int i = 1; i < 10; i++) {
        result = baseName + i;
        if (entity.GetMember(result) == null)
          return result; 
      }
      Util.Throw("Failed to generate property name for entity {0}, base name {1}", entity.Name, baseName);
      return null; 
    }

    private Type CreateDummyEntityType(string entityTypeName) {
      var fullName = _tempNamespace + "." + entityTypeName;
      var typeAttrs = TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract;
      var typeBuilder = _entityModel.ClassesAssembly.ModuleBuilder.DefineType(fullName, typeAttrs);
      return typeBuilder.CreateType();
    }

    //We have one-module per area, so we find module by associated area's schema 
    private EntityModule GetModule(string schema) {
      //Account for providers that do not support schemas (SQL CE, SQLite)
      if(schema == null && !_dbSettings.ModelConfig.Driver.Supports(DbFeatures.Schemas))
        return _app.Modules.FirstOrDefault(); 
      var module = _app.Modules.FirstOrDefault(m => m.Area.Name.Equals(schema, StringComparison.OrdinalIgnoreCase));
      Util.Check(module != null, "EntityModule for schema {0} not found.", schema);
      return module;
    }

    DbTypeInfo _guidTypeInfo;
    private Type GetMemberType(DbColumnInfo colInfo) {
      var nullable = colInfo.Flags.IsSet(DbColumnFlags.Nullable);
      var typeInfo = colInfo.TypeInfo;
      var mType = typeInfo.VendorDbType.ColumnOutType;
      if(mType.IsValueType && nullable)
        mType = ReflectionHelper.GetNullable(mType);
      Type forcedType;
      if(_config.ForceDataTypes.TryGetValue(colInfo.ColumnName, out forcedType)) {
        mType = forcedType;
        return mType;
      }
      if(mType == typeof(byte[])) {
        var changeToGuid =
          _config.Options.IsSet(DbFirstOptions.Binary16AsGuid) && typeInfo.Size == 16 ||
          _config.Options.IsSet(DbFirstOptions.BinaryKeysAsGuid) && colInfo.Flags.IsSet(DbColumnFlags.PrimaryKey | DbColumnFlags.ForeignKey);
        if(changeToGuid) {
          _guidTypeInfo = _guidTypeInfo ?? _dbSettings.ModelConfig.Driver.TypeRegistry.GetDbTypeInfo(typeof(Guid), 0);
          colInfo.TypeInfo = _guidTypeInfo;
          mType = typeof(Guid);
        }
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
