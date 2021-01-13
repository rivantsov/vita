using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Data;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Utilities;
using Vita.Entities.Logging;
using Vita.Entities.Model.Construction;
using Vita.Data.Driver.TypeSystem;

namespace Vita.Tools.DbFirst {

  /// <summary>Reconstructs the entity model from the raw DbModel loaded from the database. 
  /// The constructed entity model may be used for generating the entity interfaces as c# source file. </summary>
  public class DbFirstAppBuilder {


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

    static int _callCount; // artificial counter to generate unique temp namespaces for multiple calls
    string _tempNamespace; //temp namespace for generating interface Types; 

    public DbFirstAppBuilder(IProcessFeedback feedback) {
      _feedback = feedback; 
    }


    public EntityApp Build(DbFirstConfig config) {
      _config = config;
      _app = new EntityApp();
      var log = new BufferedLog();
      _dbSettings = new DbSettings(_config.Driver, config.Driver.GetDefaultOptions(), _config.ConnectionString);
      // create loader and setup filter
      var modelLoader = _config.Driver.CreateDbModelLoader(_dbSettings, log);
      modelLoader.SetSchemasSubset(_config.Schemas); 
      //actually load model
      _dbModel = modelLoader.LoadModel();
      Util.Check(_dbModel.Tables.Count() > 0, "No tables found in the database. Code generation aborted.");
      // Prepare type generator
      _tempNamespace = "_dummy_" + _callCount++; // artificial unique namespace for dummy interface types
      // Construct model setup and model
      GenerateModulesAndAreas(); 
      _entityModel = new EntityModel(_app);
     // EntityModelBuilder.SetModel(_app, _entityModel); 
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
      var entNames = new StringSet(); 
      _typeRegistry = _dbModel.Driver.TypeRegistry;
      var viewPrefix = "v";
      var tablePrefix = string.Empty;
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
        //track uniqueness of type names - we might have trouble if we have 2 tables with the same name in different schemas 
        if(entNames.Contains(entName))
          entName = entName + "_" + table.Schema;
        var entType = typeof(object); // CreateDummyEntityType(entName); //dummy type, just to have unique type instance
        // we add only entity types for tables; views are ignored (we do not have queires to create view definitions)
        if (table.Kind == EntityKind.Table)
          module.Entities.Add(entType); // register type in module
        // Note: we generate entity interfaces for Views, but do not register them as entities
        var ent = new EntityInfo(module, entType, table.Kind);
        ent.TableName = table.TableName;
        ent.Name = entName; 
        table.Entity = ent; 
        _entityModel.RegisterEntity(ent);
        entNames.Add(entName);
        // generate entity members
        foreach(var col in table.Columns) {
          var nullable = col.Flags.IsSet(DbColumnFlags.Nullable);
          var memberDataType = GetMemberType(col);
          var memberName = CheckMemberName(DbNameToCsName(col.ColumnName), ent);
          var member = col.Member = new EntityMemberInfo(ent, EntityMemberKind.Column, memberName, memberDataType);
          member.ColumnName = col.ColumnName;
          // member is added to ent.Members automatically in constructor
          if (nullable)
            member.Flags |= EntityMemberFlags.Nullable; // in case it is not set (for strings)
          if(col.Flags.IsSet(DbColumnFlags.Identity)) {
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
          if(unlimited)
            member.Flags |= EntityMemberFlags.UnlimitedSize;
          var typeDef = col.TypeInfo.TypeDef;

          // Detect if we need to set explicity DbType or DbTypeSpec in member attribute
          var dftMapping = _typeRegistry.GetDbTypeInfo(member);
          if(col.TypeInfo.Matches(dftMapping))
            continue; //no need for explicit DbTypeSpec
          //DbTypeMapping is not default for this member - we need to specify DbType or TypeSpec explicitly
          member.ExplicitDbTypeSpec = col.TypeInfo.DbTypeSpec;
        }
      }//foreach table
    }//method

    private void SetupPrimaryKeys() {
      foreach (var table in _dbModel.Tables) {
        if(table.Entity == null || table.Kind == EntityKind.View)
          continue; //ignore this table
        var ent = table.Entity;
        ent.PrimaryKey = new EntityKeyInfo(ent, KeyType.PrimaryKey);
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
      var fkAutoIndexed = _dbModel.Driver.Supports(DbFeatures.ForeignKeysAutoIndexed);
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
          var entKey = key.EntityKey = new EntityKeyInfo(table.Entity, key.KeyType); //added automatically to entity.Keys
          entKey.Name = key.Name;
          foreach(var keyCol in key.KeyColumns)
            entKey.ExpandedKeyMembers.Add(new EntityKeyMemberInfo(keyCol.Column.Member, keyCol.Desc));
          if (entKey.KeyType.IsSet(KeyType.Index) && supportsIncludeOrFilter) {
            if (key.Filter != null && !string.IsNullOrEmpty(key.Filter.DefaultSql)) {
              entKey.IndexFilter =  ParseIndexFilter(key.Filter.DefaultSql, entKey.Entity);
            }
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

    private EntityFilter ParseIndexFilter(string filterSql, EntityInfo entity) {
      // The filter coming from database has references to column names (without braces like {colName})
      // The filter in Index attribute references entity members using {..} notation. 
      // Technically we should try to convert filter from db into attribute form, but we don't; maybe later
      var entFilter = new EntityFilter() { Template = StringTemplate.Parse(filterSql) };
      return entFilter; 
    }

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
          var refMember = new EntityMemberInfo(ent, EntityMemberKind.EntityRef, memberName, targetEnt.EntityType);
          if (constr.CascadeDelete)
            refMember.Flags |= EntityMemberFlags.CascadeDelete;
          // create foreign key
          var fromDbKey = constr.FromKey;
          var fromEntKey = fromDbKey.EntityKey = new EntityKeyInfo(ent, KeyType.ForeignKey, refMember); // added to ent.Keys automatically
          fromEntKey.Name = fromDbKey.Name; 
          foreach(var fromKeyCol in fromDbKey.KeyColumns) {
            fromEntKey.ExpandedKeyMembers.Add(new EntityKeyMemberInfo(fromKeyCol.Column.Member, fromKeyCol.Desc)); 
            fromKeyCol.Column.Member.Flags |= EntityMemberFlags.ForeignKey;
            fromKeyCol.Column.Member.ForeignKeyOwner = refMember;
            if (fromKeyCol.Column.Member.Flags.IsSet(EntityMemberFlags.Nullable))
              refMember.Flags |= EntityMemberFlags.Nullable; 
          }
          refMember.ReferenceInfo = new EntityReferenceInfo(refMember, fromEntKey, targetEnt.PrimaryKey);
          // Add lists on parent entities only for ref members that have indexes
          var memberIndex = refMember.Entity.Keys.FirstOrDefault(k => k.OwnerMember == refMember && k.KeyType.IsSet(KeyType.Index));
          if (addListMembers && memberIndex != null) {
            //create List member on target entity
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
      }//foreach table
    }

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
