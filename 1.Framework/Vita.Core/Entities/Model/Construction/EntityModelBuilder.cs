using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Collections;

using Vita.Common;
using Vita.Common.Graphs;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Linq;

namespace Vita.Entities.Model.Construction {
  using Binary = Vita.Common.Binary;
  using ExpressionUtil = Vita.Data.Linq.Translation.ExpressionUtil;

  public class EntityModelBuilder {
    MemoryLog _log;
    EntityApp _app;
    EntityModel _model;
    //Temporary lists
    IList<EntityInfo> _allEntities; // initial list of all entities
    Dictionary<Type, EntityInfo> _entitiesByType;
    AttributeContext _attributeContext;

    public EntityModelBuilder(EntityApp app) {
      _app = app;
      _log = _app.ActivationLog;
    }

    public EntityModel BuildModel() {
      _model = new EntityModel(_app);
      SetModel(_app, _model);  
      _attributeContext = new AttributeContext(this._model, _log);
      _allEntities = new List<EntityInfo>();
      _entitiesByType = new Dictionary<Type, EntityInfo>();

      CollectEntitiesAndViews();
      ProcessReplacements();
      RegisterEntities();
      VerifyEntityReferences();
      BuildEntityMembers();
      _model.ModelState = EntityModelState.EntitiesConstructed;
      //Let extenders add custom members
      foreach(var iExt in _app.ModelExtenders)
        iExt.Extend(_model);
      //  _app.AppEvents.OnInitializing(EntityAppInitStep.CustomizeModel); 
      //Attributes
      CollectAllAttributes();
      // Basic infrastructure - PKs, FKs
      ProcessSpecialAttributes();
      if (Failed())
        return _model;
      // We have to run expansion twice. First time right after processing special attributes - to expand foreign keys. 
      // Indexes (specified in non-special attributes) may refer to columns created from KeyColumns in foreign keys of entity references, 
      // so these keys must be expanded. The second time we expand key members to expand indexes themselves.
      ExpandEntityKeyMembers();
      ProcessNonSpecialModelAttributes();
      // Moved index-building code from attribute to this class; doing it here, after processing the attributes
      BuildIndexes();
      if(Failed())
        return _model;
      ExpandEntityKeyMembers();

      ValidateIndexes(); 
      CompleteMembersSetup();
      BuildDefaultInitialValues();
      BuildEntityClasses();
      ComputeTopologicalIndexes();
      CollectEnumTypes();
      PreprocessViewQueries(); 
      if (Failed())
        return _model;
      //commands
      BuildCommands();
      //fire event
      _app.AppEvents.OnInitializing(EntityAppInitStep.EntityModelConstructed);
      _model.ModelState = EntityModelState.Ready;
      return _model; 
    }//method

    internal void LogError(string message, params object[] args) {
      _log.Error(message, args);
    }
    private bool Failed() {
      return _log.HasErrors();
    }

    //Collects registered entities - creates EntityInfo objects for each entity and adds them to Model's Entities set. 
    private void CollectEntitiesAndViews() {
      // Collect initial entities
      foreach(var module in _app.Modules) {
        foreach(var entType in module.Entities)
          AddEntity(module, entType);
        foreach(var view in module.Views) {
          if (!ValidateViewDefinition(view)) 
            continue;
          var entInfo = AddEntity(module, view.EntityType, EntityKind.View);
          entInfo.ViewDefinition = view;
          view.Command.TargetEntity = entInfo; 
          if (!string.IsNullOrEmpty(view.Name))
            entInfo.TableName = view.Name;
        }//foreach view
      }
    }//method

    private bool ValidateViewDefinition(ViewDefinition view) {
      bool ok = true; 
      if (!view.EntityType.IsInterface) {
        LogError("View definition error ({0}): view entity must be an interface.", view.EntityType);
        ok = false; 
      }
      var queryOutType = view.Command.ResultType;
      if (!queryOutType.IsGenericType) {
        LogError("View definition error ({0}): query must return IQueryable<T> generic type.", view.EntityType);
        ok = false;
      }
      if (!ok) return false; 
      var outObjType = queryOutType.GetGenericArguments()[0];
      if (outObjType == view.EntityType)
        return true; 
      // Query output is auto type; check that its properties match properties of view entity
      var entProps = view.EntityType.GetAllProperties();
      foreach(var entProp in entProps) {
        var outProp = outObjType.GetProperty(entProp.Name);
        if (outProp == null) {
          LogError("View definition error ({0}): view property '{1}' not returned by the query.", view.EntityType, entProp.Name);
          ok = false;
          continue; //next prop 
        }
        if (outProp.PropertyType != entProp.PropertyType) {
          LogError("View definition error ({0}): data type for view property '{1}' ({2} ) does not match query output property type ({3}) .",
            view.EntityType, entProp.Name, entProp.PropertyType, outProp.PropertyType);
          ok = false;
        }
      }// foeach entProp
      return ok; 
    }

    // Verify that entities referenced in properties are registered.
    private void VerifyEntityReferences() {
      //initialize all-entities list 
      Type typeInList;
      foreach (var entInfo in _allEntities) {
        var props = entInfo.EntityType.GetAllProperties();
        foreach (var prop in props) {
          var propType = prop.PropertyType; 
          if (_model.IsRegisteredEntityType(propType))
            CheckReferencedEntity(propType, prop.Name, entInfo);
          else if (TryGetEntityTypeFromList(propType, out typeInList)) {
            CheckReferencedEntity(typeInList, prop.Name, entInfo);
            //For many-to-many, we may need to add LinkEntity
            var m2mAttr = prop.GetAttribute<ManyToManyAttribute>();
            if (m2mAttr != null && _model.IsEntity( m2mAttr.LinkEntity)) 
              CheckReferencedEntity(m2mAttr.LinkEntity, prop.Name, entInfo); 
          }// else if IsEntityList
        }//foreac prop
      }//for i
    }//method

    private bool TryGetEntityTypeFromList(Type listType, out Type entityType) {
      entityType = null;
      if (!listType.IsGenericType)
        return false;
      var genType = listType.GetGenericTypeDefinition();
      if (!typeof(IList<>).IsAssignableFrom(genType))
        return false;
      entityType = listType.GetGenericArguments()[0];
      if (_model.IsEntity(entityType))
        return true;
      entityType = null;
      return false;
    }


    private EntityInfo GetEntity(Type entityType) {
      EntityInfo entInfo;
      _entitiesByType.TryGetValue(entityType, out entInfo);
      return entInfo;
    }

    private void CheckReferencedEntity(Type entityType, string propertyName, EntityInfo owner) {
      EntityInfo entInfo;
      if (!_entitiesByType.TryGetValue(entityType, out entInfo)) 
        LogError("Property {0}.{1}: referenced entity type {2} is not registered as an entity.", owner.EntityType.Name, propertyName, entityType.Name);
    }

    private EntityInfo AddEntity(EntityModule module, Type entityType, EntityKind kind = EntityKind.Table) {
      EntityInfo entInfo; 
      if (_entitiesByType.TryGetValue(entityType, out entInfo)) 
        return entInfo;
      entInfo = new EntityInfo(module, entityType, kind);
      _allEntities.Add(entInfo);
      _entitiesByType[entityType] = entInfo;
      return entInfo; 
    }

    private void ProcessReplacements() {
      var oldCount = _allEntities.Count;
      // 1. Go thru replacements, find/create entity info for "new" entities, 
      //    and register this entity info under the key of replaced entity type
      foreach (var replInfo in _model.App.Replacements) {
        var newEntInfo = GetEntity(replInfo.NewType);
        var oldEntInfo = GetEntity(replInfo.ReplacedType);
        if(oldEntInfo == null && newEntInfo == null) {
          LogError("Replacing entity {0}->{1}. Error: Register at least one of the entity types with an EntityModule.",
            replInfo.ReplacedType, replInfo.NewType);
          continue; 
        }
        EntityModule targetModule = newEntInfo == null ? oldEntInfo.Module : newEntInfo.Module; 

        //Register new entity if necessary
        if (newEntInfo == null) {
          newEntInfo = AddEntity(oldEntInfo.Module, replInfo.NewType);
        }
        _entitiesByType[replInfo.ReplacedType] = newEntInfo;
        newEntInfo.ReplacesTypes.Add(replInfo.ReplacedType); 
        
        if (oldEntInfo != null)
          oldEntInfo.ReplacedBy = newEntInfo;
      }//foreach replInfo

      // 2. Trace replacedBy reference, find final replacing type and register entity info for final type under the "replaced type" key
      foreach (var entInfo in _allEntities) {
        if (entInfo.ReplacedBy == null) continue; 
        entInfo.ReplacedBy = GetFinalReplacement(entInfo);
        entInfo.ReplacedBy.ReplacesTypes.Add(entInfo.EntityType);
      }
    }

    private void RegisterEntities() {
      foreach (var entInfo in _allEntities) {
        var otherEnt = _model.GetEntityInfo(entInfo.FullName);
        if (otherEnt != null) {
          LogError("Duplicate entity full name: entity {0} is already registered.", entInfo.FullName);
          continue;
        }
        _model.RegisterEntity(entInfo);
      }
    }

    //Note: returns entityInfo if there is no replacement
    private EntityInfo GetFinalReplacement(EntityInfo entityInfo) {
      var current = entityInfo;
      while (current.ReplacedBy != null)
        current = current.ReplacedBy;
      return current;
    }

    private void BuildEntityMembers() {
      //Now build entity info for type in final types
      foreach (var entInfo in _model.Entities) {
        //Create data members
        var props = entInfo.EntityType.GetAllProperties();
        EntityMemberInfo member; 
        foreach (var prop in props) {
          MemberKind kind;
          if (TryGetMemberKind(entInfo, prop, out kind)) {
            member = new EntityMemberInfo(entInfo, kind, prop); //member is added to members automatically
          } //else - we skip property, the TryGetMemberKind should have logged the message
        }
      }//foreach entType
    }

    private bool TryGetMemberKind(EntityInfo entity, PropertyInfo property, out MemberKind kind) {
      var dataType = property.PropertyType;
      kind = MemberKind.Column;
      if (dataType.IsValueType || dataType == typeof(string)) 
        return true; 
      var genType = dataType.IsGenericType ? dataType.GetGenericTypeDefinition() : null;
      if (genType == typeof(Nullable<>)) 
        return true; 
      if (_model.IsEntity(dataType)) {
        kind = MemberKind.EntityRef;
        var target = _model.GetEntityInfo(dataType);
        if (target != null)
          return true;
        LogError("Invalid entity reference, type {2} is not registered as an entity. Entity member: {0}.{1}", entity.Name, property.Name, dataType);
        return false;         
      }
      if (genType == typeof(IList<>)) {
        kind = MemberKind.EntityList;
        return true;
      }
      // properly report common mistake
      if (genType == typeof(List<>)) {
        this.LogError("Invalid entity member {0}.{1}. Use IList<T> interface for list members. ", entity.Name, property.Name);
        return false; 
      }
      //default: Column
      return true; //Column; there are some specific types that turn into column (Binary for ex)
    }

    private void CollectAllAttributes() {
      //1. Collection attributes on entities and members themselves
      // View entities are special - we do NOT inherit attributes on entity (if view entity is derived from normal entity);
      // we inherit attribute on members except keys and indexes
      foreach (var entity in _model.Entities) {
        bool isTable = entity.Kind == EntityKind.Table;
        entity.Attributes.AddRange(entity.EntityType.GetAllAttributes(inherit: isTable));
        foreach (var member in entity.Members) {
          var mInfo = member.ClrMemberInfo;
          if (mInfo == null) continue;
          if (isTable)
            member.AddAttributes(mInfo.GetCustomAttributes(inherit: true));
          else {
            Func<Attribute, bool> isIndexOrPk = a => (a is IndexAttribute) || (a is PrimaryKeyAttribute);
            //Get ALL attributes from member declared on Type; if member  is inherited, add attributes from base class member,
            // but remove index attributes and key attributes
            var allAttrs = mInfo.GetCustomAttributes(inherit: false).OfType<Attribute>();
            if (mInfo.DeclaringType != entity.EntityType) //it is inherited member
              allAttrs = allAttrs.Where(a => !isIndexOrPk(a));
            member.AddAttributes(allAttrs);
          }
        } //foreach member
      }//foreach entity   

      // 2. Collect attributes from companion types
      foreach (var companion in _model.App.CompanionTypes) {
        Type entType = null;
        // First see if ForEntity attr is specified on companion type; if not, find target entity in inheritance chain
        // - companion types usually inherit from entities they accompany
        var feAtt = companion.GetAttribute<ForEntityAttribute>();
        if (feAtt != null) {
          entType = feAtt.EntityType;
        }
        if (entType == null)
          entType = FindBaseEntity(companion);
        if (entType == null) {
          LogError("Could not find target Entity type for companion type {0}.", companion);
          continue;
        }
        //Find entity info
        var entity = _model.GetEntityInfo(entType);
        if (entity == null) {
          LogError("Entity type {0} for companion type {1}  is not registered as Entity.", entType, companion);
          continue;
        }
        entity.CompanionTypes.Add(companion);
        //Add attributes
        entity.Attributes.AddRange(companion.GetAllAttributes(inherit: false));
        //Add attributes from members of Companion Type. Note we use DeclaredOnly to catch only those that are declared 
        // by the type itself, not inherited.
        const BindingFlags propBindingFlags = BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var props = companion.GetProperties(propBindingFlags);
        foreach (var prop in props) {
          var member = entity.GetMember(prop.Name);
          if (member != null)
            member.AddAttributes(prop.GetCustomAttributes(false));
        }//foreach prop
      }//foreach cpmnType
    }

    private Type FindBaseEntity(Type type) {
      var types = type.GetInterfaces();
      var ent = types.FirstOrDefault(t => _model.IsEntity(t));
      return ent;
    }

    private void ProcessSpecialAttributes() {
      var tableEntities = _model.Entities.ToList(); //.Where(e => e.Kind == EntityKind.Table).ToList(); 
      // Pass 1
      foreach (var entity in tableEntities) {
        FindAndApplyAttribute<EntityAttribute>(entity);
        FindAndApplyAttribute<OldNamesAttribute>(entity);
        foreach (var member in entity.Members) {
          //Apply NoColumn attribute - it might change member kind
          FindAndApplyAttribute<NoColumnAttribute>(member);
          // Apply nullable attribute
          FindAndApplyAttribute<NullableAttribute>(member);
          //apply Size attr if present; Column attr might overwrite size value
          FindAndApplyAttribute<SizeAttribute>(member);
          if (member.Size == 0)
            SetDefaultMemberSize(member);
          //apply column attr if present; we apply it here rather than inside switch, to detect improper use
          FindAndApplyAttribute<ColumnAttribute>(member);
          //apply oldNamescolumn attr if present; 
          FindAndApplyAttribute<OldNamesAttribute>(member);
          //check size and set IsMemo attribute
          if (member.Size < 0)
            member.Flags |= EntityMemberFlags.UnlimitedSize; 
        }//foreach member
      }//foreach entity -- end pass 1
      if (Failed())
        return;

      // Pass 2 - primary keys. PK may reference members using column names, so we do it after applying Column attribute to all members
      foreach(var entity in tableEntities) {
        if(entity.Kind == EntityKind.View)
          continue;
        bool pkAttrFound = false; 
        var entPkAttr = entity.GetAttribute<PrimaryKeyAttribute>();
        if (entPkAttr != null) {
          entPkAttr.Apply(_attributeContext, entPkAttr, entity);
          pkAttrFound = true;           
        }
        foreach (var member in entity.Members) {
          pkAttrFound |= FindAndApplyAttribute<PrimaryKeyAttribute>(member);
        }
        if (pkAttrFound) 
          continue; //next entity
        if (entity.PrimaryKey == null) {
            LogError("Entity {0} has no PrimaryKey attribute.", entity.FullName);
        }
      }//foreach entity
      if(Failed())
        return;

      // Pass 3 - entity references. Relies on PKs already created
      foreach(var entity in tableEntities) {
       // if(entity.Kind == EntityKind.View) continue;
        foreach(var member in entity.Members) {
          if (member.Kind != MemberKind.EntityRef) continue; 
          var erAttr = member.GetAttribute<EntityRefAttribute>();
          if (erAttr == null) {
            erAttr = new EntityRefAttribute();
            member.Attributes.Add(erAttr);
          }
          erAttr.Apply(_attributeContext, erAttr, member); 
        }//foreach member
      }// foreach entity

      // Pass 4 - entity lists. Assumes entity references are processed
      foreach(var entity in tableEntities) {
        foreach (var member in entity.Members) {
          if (member.Kind != MemberKind.EntityList) continue;
          // Check many-to-many
          if (FindAndApplyAttribute<ManyToManyAttribute>(member)) continue;
          // try to find and apply OneToMany
          if (FindAndApplyAttribute<OneToManyAttribute>(member)) continue;
          // If neither attribute is found, create OneToMany and apply it
          var attr = new OneToManyAttribute();
          member.Attributes.Add(attr);
          attr.Apply(_attributeContext, attr, member); 
        }
      }
      
    }//method

    private void SetDefaultMemberSize(EntityMemberInfo member) {
      var type = member.DataType;
      // Set default for string size, might be changed later by attributes
      if (type == typeof(string))
        member.Size = member.Entity.Area.App.DefaultStringLength;
      else if (type == typeof(char))
        member.Size = 1;
      else if (type == typeof(byte[]) || type == typeof(Binary))
        member.Size = 128;
    }
    private bool FindAndApplyAttribute<TAttr>(EntityMemberInfo member) where TAttr : EntityModelAttributeBase {
      var attr = member.GetAttribute<TAttr>();
      if (attr == null) return false;
      attr.Apply(_attributeContext, attr, member);
      return true;
    }
    private bool FindAndApplyAttribute<TAttr>(EntityInfo entity) where TAttr : EntityModelAttributeBase {
      var attr = entity.GetAttribute<TAttr>();
      if (attr == null) return false;
      attr.Apply(_attributeContext, attr, entity);
      return true;
    }



    #region AttributeTuple inner class
    class AttributeTuple {
      public IAttributeHandler Handler; 
      public Attribute Attribute;
      public EntityInfo Entity;
      public EntityMemberInfo Member;

      public override string ToString() {
        return Attribute.ToString();
      }
      public static int CompareOrder(AttributeTuple x, AttributeTuple y) {
        return ((int)x.Handler.ApplyOrder).CompareTo((int)y.Handler.ApplyOrder);
      }
      public void Apply(AttributeContext context) {
        var attrDesc = Attribute.GetType().Name;
        if (this.Entity != null)
          try {
            this.Handler.Apply(context, Attribute, Entity);
          } catch (Exception ex) {
            context.Log.Error("Exception thrown when applying attribute {0} on entity {1}: {2}", attrDesc, Entity.Name, ex.Message);
          }
        if (this.Member != null)
          try {
            Handler.Apply(context, Attribute, Member);
          } catch (Exception ex) {
            context.Log.Error("Exception thrown when applying attribute {0} on property {1}.{2}: {3}", attrDesc, Member.Entity.Name, Member.MemberName, ex.Message);
          }
      }
    }
    #endregion

    //SpecialModelAttributes are processed by model builder according to its own logic. Like PrimaryKey attr is already processsed when we were building
    // entities. Here we process all other, non-special attributes
    private void ProcessNonSpecialModelAttributes() {
      //Collect model attributes
      var attrTuples = new List<AttributeTuple>();
      foreach(var ent in _allEntities) {
        var nsAttrs = ent.Attributes.Where(a => !(a is SpecialModelAttribute)); 
        attrTuples.AddRange(nsAttrs.Select(a => new AttributeTuple() { Attribute = a, Entity = ent, Handler = GetAttributeHandler(a)}).Where(t => t.Handler != null));
        foreach (var member in ent.Members) {
          nsAttrs = member.Attributes.Where(a => !(a is SpecialModelAttribute)); 
          attrTuples.AddRange(nsAttrs.Select(a => new AttributeTuple() { Attribute = a, Member = member, Handler = GetAttributeHandler(a) }).Where(t => t.Handler != null));
        }
      }//foreach ent
      //Sort
      attrTuples.Sort(AttributeTuple.CompareOrder);
      //Apply
      attrTuples.ForEach(t => t.Apply(_attributeContext));
    }//method

    private IAttributeHandler GetAttributeHandler(Attribute attribute) {
      IAttributeHandler handler = attribute as IAttributeHandler;
      if (handler != null)
        return handler;
      CustomAttributeHandler customHandler;
      if (_app.AttributeHandlers.TryGetValue(attribute.GetType(), out customHandler))
        return customHandler;
      return null; 
    }

    private void ValidateIndexes() {
      foreach (var entity in _model.Entities) {
        //Clustered indexes
        var ciKeys = entity.Keys.FindAll(k => k.KeyType.IsSet(KeyType.Clustered));
        switch (ciKeys.Count) {
          case 0:   break; //nothing to do
          case 1:
            entity.Flags |= EntityFlags.HasClusteredIndex;
            break;
          default:
            LogError("More than one clustered index specified on entity {0}", entity.FullName);
            break;
        } //switch
      }//foreach entity
    }//method

    // Some keys may contain members that are entity references;
    // We need to replace them with foreign key members (which are values actually saved in database). 
    // The problem is that we cannot simply expand primary keys, then foreign keys, and then all others
    // Primary keys may contain entity references, in effect containing members of foreign keys which should be expanded first 
    // from primary keys of target references. So this is an iterative multi-pass process of expansion
    private void ExpandEntityKeyMembers() {
      var allKeys = _model.Entities.SelectMany(e => e.Keys).Where(k => !k.IsExpanded).ToList();
      var currList = allKeys; 
      //Make 5 rounds 
      for (int i = 0; i < 10; i++) {
        var newList = new List<EntityKeyInfo>();
        foreach (var key in currList)
          if (!ExpandKey(key)) newList.Add(key);
        if (newList.Count == 0)
          return; //we are done, all keys are expanded
        //set currList from newList, and go for another iteration
        currList = newList; 
      }//for i
      //if we are here, we failed to expand all keys
      var keyDescr = string.Join(", ", currList);
      LogError("Invalid key composition, circular references in keys: {0} ", keyDescr);
    }

    private bool ExpandKey(EntityKeyInfo key) {
      if (key.IsExpanded) 
        return true;
      if(key.KeyType.IsSet(KeyType.ForeignKey))
        return ExpandForeignKey(key);
      var notYet = key.KeyMembers.Any(km => km.Member.Kind == MemberKind.EntityRef && !km.Member.ReferenceInfo.FromKey.IsExpanded) ||
                   key.IncludeMembers.Any(m => m.Kind == MemberKind.EntityRef && !m.ReferenceInfo.FromKey.IsExpanded);
      if(notYet)
        return false;
      key.ExpandedKeyMembers.Clear();
      //Any other key type; first key members, then include members 
      foreach (var keyMember in key.KeyMembers) {
        if (keyMember.Member.Kind == MemberKind.EntityRef) {
          var fkey = keyMember.Member.ReferenceInfo.FromKey;
          key.ExpandedKeyMembers.AddRange(fkey.ExpandedKeyMembers);
        } else 
          key.ExpandedKeyMembers.Add(keyMember);
      }//foreach 
      // include members
      foreach(var member in key.IncludeMembers) {
        if(member.Kind == MemberKind.EntityRef) {
          var fkey = member.ReferenceInfo.FromKey;
          key.ExpandedIncludeMembers.AddRange(fkey.ExpandedKeyMembers.Select(km => km.Member));
        } else
          key.ExpandedIncludeMembers.Add(member);
      }//foreach member
      key.IsExpanded = true;
      key.HasIdentityMember = key.ExpandedKeyMembers.Any(m => m.Member.Flags.IsSet(EntityMemberFlags.Identity));
      return true; 
    }

    //FK expansion is a special case - we expand members from target expanded members (of target PrimaryKey)
    private bool ExpandForeignKey(EntityKeyInfo key) {
      var refMember = key.KeyMembers[0].Member;
      var nullable = refMember.Flags.IsSet(EntityMemberFlags.Nullable);
      var isPk = refMember.Flags.IsSet(EntityMemberFlags.PrimaryKey);
      var refInfo = refMember.ReferenceInfo;
      if (!refInfo.ToKey.IsExpanded)
        return false;
      var pkKeyMembers = refInfo.ToKey.ExpandedKeyMembers;
      // Check if we have explicitly specified names in ForeignKey attribute
      string[] fkNames = null;
      if (!string.IsNullOrEmpty(refInfo.ForeignKeyColumns)) {
        fkNames = refInfo.ForeignKeyColumns.SplitNames(',', ';');
        if (fkNames.Length != pkKeyMembers.Count) {
          LogError("Invalid ForeignKey specification in property {0}.{1}: # of columns ({2}) does not match # of columns ({3}) in target primary key.", 
                   refMember.Entity.FullName, refMember.MemberName, fkNames.Length, pkKeyMembers.Count);
          return true; 
        }              
      }
      // build members
      for (var i = 0; i < pkKeyMembers.Count; i++) {
        var targetMember = pkKeyMembers[i].Member;
        var fkMemberName = fkNames == null ? refMember.MemberName + "_" + targetMember.MemberName : fkNames[i];
        var memberType = targetMember.DataType;
        //If reference is nullable, then force member to be nullable too - and flip c# type to nullable
        if (nullable && (memberType.IsValueType || memberType.IsEnum)) {
          //CLR type is not nullable - flip it to nullable
          memberType = ReflectionHelper.GetNullable(memberType);
        }
        var fkMember = new EntityMemberInfo(key.Entity, MemberKind.Column, fkMemberName, memberType);
        fkMember.Flags |= EntityMemberFlags.ForeignKey;
        fkMember.ExplicitDbType = targetMember.ExplicitDbType;
        fkMember.ExplicitDbTypeSpec = targetMember.ExplicitDbTypeSpec;
        if (targetMember.Size > 0)
          fkMember.Size = targetMember.Size;
        if (targetMember.Flags.IsSet(EntityMemberFlags.AutoValue)) {
          fkMember.Flags |= EntityMemberFlags.AutoValue;
        }
        fkMember.ForeignKeyOwner = refMember;
        if (nullable)
          fkMember.Flags |= EntityMemberFlags.Nullable;
        if (isPk)
          fkMember.Flags |= EntityMemberFlags.PrimaryKey;
        //copy old names
        if (key.OwnerMember.OldNames != null)
          fkMember.OldNames = key.OwnerMember.OldNames.Select(n => n + "_" + targetMember.MemberName).ToArray(); 
        key.ExpandedKeyMembers.Add(new EntityKeyMemberInfo(fkMember, false));
      }//foreach targetMember
      key.IsExpanded = true;
      return true;
    }

    //Important - this should be done after processing attributes
    private void CompleteMembersSetup() {
      foreach (var ent in _model.Entities) {
        ent.PersistentValuesCount = 0;
        ent.TransientValuesCount = 0;
        var hasUpdatableMembers = false;
        foreach (var member in ent.Members) {
          if (member.Kind == MemberKind.Column) {
            member.ValueIndex = ent.PersistentValuesCount++;
            if (member.Flags.IsSet(EntityMemberFlags.PrimaryKey))
              member.Flags |= EntityMemberFlags.NoDbUpdate;
            if (!member.Flags.IsSet(EntityMemberFlags.NoDbUpdate))
              hasUpdatableMembers = true;
          } else
            member.ValueIndex = ent.TransientValuesCount++;
          if (member.Kind == MemberKind.EntityRef) {
            member.ReferenceInfo.CountCommand = BuildGetCountForEntityRef(member.ReferenceInfo);
          }
        }//foreach member
        if (!hasUpdatableMembers)
          ent.Flags |= EntityFlags.NoUpdate;
        ent.RefMembers = ent.Members.Where(m => m.Kind == MemberKind.EntityRef).ToList(); 
      }//foreach ent
    }

    private void BuildIndexes() {
      foreach(var ent in _model.Entities) {
        //get all index attributes - on entity and on members
        var indAttrs = ent.Attributes.Where(a => a is IndexAttribute).ToList();
        var memberIndAttrs = ent.Members.SelectMany(m => m.Attributes.Where(a => a is IndexAttribute));
        indAttrs.AddRange(memberIndAttrs); 
        foreach(IndexAttribute attr in indAttrs)
          BuildIndex(ent, attr); 
      }
    }

    private void BuildIndex(EntityInfo entity, IndexAttribute attribute) {
      if(string.IsNullOrWhiteSpace(attribute.MemberNames)) {
        LogError("Entity {0}: Index attribute on entity may not have empty member list.", entity.Name);
        return;
      }
      //Build member list
      bool err = false;
      var indexMembers = EntityAttributeHelper.ParseMemberNames(entity, attribute.MemberNames, ordered: true,
        errorAction: spec => {
          LogError("Entity {0}: invalid index spec '{1}'. Property not found or invalid asc/desc specifier.", entity.Name, spec);
          err = true;
        });
      if(err)
        return;
      // Check include fields
      IList<EntityKeyMemberInfo> includes = null;
      if(!string.IsNullOrWhiteSpace(attribute.IncludeMembers)) {
        includes = EntityAttributeHelper.ParseMemberNames(entity, attribute.IncludeMembers, ordered: false,
                  errorAction: spec => {
                    LogError("Entity {0}: invalid index spec '{1}'. Property not found.", entity.Name, spec);
                    err = true;
                  });
      }
      if(err)
        return;
      //build index name
      var indName = attribute.IndexName;
      if(string.IsNullOrWhiteSpace(indName)) {
        var mNamesSuffix = string.Join(string.Empty, indexMembers.Select(im => im.Member.MemberName));
        if(mNamesSuffix.Length > 40) //protect against too long names
          mNamesSuffix = "_" + Math.Abs(Util.StableHash(mNamesSuffix)).ToString();
        indName = GetIndexNamePrefix(attribute.KeyType) + "_" + entity.Name + "_" + mNamesSuffix;
        indName = entity.GetUniqueKeyName(indName);
      }
      //Create index
      var index = new EntityKeyInfo(indName, attribute.KeyType, entity);
      index.Alias = attribute.Alias ?? indName;
      //copy members
      index.KeyMembers.AddRange(indexMembers);
      if(includes != null)
        index.IncludeMembers.AddRange(includes.Select(km => km.Member));
      index.Filter = attribute.Filter;
    }//method

    protected string GetIndexNamePrefix(KeyType indexType) {
      var result = "IX";
      if(indexType.IsSet(KeyType.Clustered))
        result += "C";
      if(indexType.IsSet(KeyType.Unique))
        result += "U";
      return result;
    }

    private void BuildEntityClasses() {
      var classBuilder = new EntityClassBuilder(); 
      classBuilder.BuildEntityClasses(_model);
    }

    private void CollectEnumTypes() {
      var typeSet = new HashSet<Type>(); 
      foreach (var ent in _model.Entities)
        foreach (var member in ent.Members) {
          var type = member.DataType; 
          if (type.IsEnum && !typeSet.Contains(type)) {
            typeSet.Add(type);
            _model.AddEnumType(type);
          }
        }//foreach member
    }


    private void BuildCommands() {
      var commandBuilder = new EntityCommandBuilder(_model, _log);
      foreach (var entity in _model.Entities) {
        var entType = entity.EntityType;
        var cmds = entity.CrudCommands = new EntityCrudCommands();
        cmds.SelectAll = commandBuilder.BuildCrudSelectAllCommand(entity);
        cmds.SelectAllPaged = commandBuilder.BuildCrudSelectAllPagedCommand(entity);
        if (entity.Kind == EntityKind.View)
          continue; 
        cmds.SelectByPrimaryKey = entity.PrimaryKey.SelectByKeyCommand = commandBuilder.BuildCrudSelectByKeyCommand(entity.PrimaryKey);
        cmds.SelectByPrimaryKeyArray = entity.PrimaryKey.SelectByKeyArrayCommand = commandBuilder.BuildCrudSelectByKeyArrayCommand(entity.PrimaryKey);
        cmds.Insert = commandBuilder.BuildCrudInsertCommand(entity);
        if (!entity.Flags.IsSet(EntityFlags.NoUpdate))
          cmds.Update = commandBuilder.BuildCrudUpdateCommand(entity);
        cmds.Delete = commandBuilder.BuildCrudDeleteCommand(entity); 

        // Special member-attached commands
        foreach (var member in entity.RefMembers) {
          var fk = member.ReferenceInfo.FromKey;
          fk.SelectByKeyCommand = commandBuilder.BuildCrudSelectByKeyCommand(fk);
          fk.SelectByKeyArrayCommand = commandBuilder.BuildCrudSelectByKeyArrayCommand(fk); 
        }//foreach member
      }//foreach ent
      // loop 2 - build select commands for property lists; 
      // we do it in a separate loop because for one-to-many lists, the command is the same as select-by-key 
      // (if no Filter), so we just copy the command from there - it is created in previous loop
      foreach (var entity in _model.Entities) {
        foreach(var member in entity.Members) {
          if(member.Kind != MemberKind.EntityList) continue;
          var listInfo = member.ChildListInfo;
          if(string.IsNullOrWhiteSpace(listInfo.Filter)) {
            listInfo.SelectDirectChildList = listInfo.ParentRefMember.ReferenceInfo.FromKey.SelectByKeyCommand; // no filter, so it's just select-by-key
          } else {
            //with filter
            var nameSuffix = "For" + listInfo.OwnerMember.MemberName;
            listInfo.SelectDirectChildList = commandBuilder.BuildCrudSelectByKeyCommand(
                 listInfo.ParentRefMember.ReferenceInfo.FromKey, listInfo.Filter, nameSuffix);
          }
          if(listInfo.RelationType == EntityRelationType.ManyToMany)
            listInfo.SelectTargetListManyToMany = commandBuilder.BuildSelectTargetListManyToManyCommand(listInfo);
        }//foreach member
      } //foreach entity
    }//method


    // Note about special case: members with CascadeDelete attribute.
    // Demo case setup. 3 entities, IBook, IAuthor, and IBookAuthor as link table; IBookAuthor references IBook with CascadeDelete,
    // and references IAuthor without cascade. 
    // Because of CascadeDelete, when we delete IBook and IBookAuthor in one operation, the order of IBook vs IBookAuthor does not matter: 
    // even if IBook comes before IBookAuthor, delete will succeed because of cascade delete of IBookAuthor. 
    // The problem case is when we are deleting IBook and IAuthor, without explicitly deleting IBookAuthor. 
    // In this case IAuthor should be deleted after IBook - otherwise still existing IBookAuthor record
    // would prevent it from deleting. As there's no explicit IBookAuthor in delete set, and there's 
    // no FK links between IAuthor and IBook - then they may come to delete in any order, and trans might fail.
    // The solution is to introduce an extra direct link between IBook and IAuthor in abstract SCC node tree.
    // This extra link will ensure proper topological ordering of IBook and IAuthor.
    // Note that we still need to add link between IBookAuthor and IBook - for proper ordering of inserts.
    private void ComputeTopologicalIndexes() {
      // Run SCC algorithm
      var g = new Graph();
      //Perform SCC analysis.
      foreach (var ent in _model.Entities)
        ent.SccVertex = g.Add(ent);
      //setup links
      foreach (var ent in _model.Entities) {
        var cascadeMembers = new List<EntityMemberInfo>();
        var nonCascadeMembers = new List<EntityMemberInfo>(); 
        foreach (var member in ent.RefMembers) {
          var targetEnt = member.ReferenceInfo.ToKey.Entity;
            ent.SccVertex.AddLink(targetEnt.SccVertex);
            if (member.Flags.IsSet(EntityMemberFlags.CascadeDelete))
              cascadeMembers.Add(member);
            else
              nonCascadeMembers.Add(member); 
        }//foreach member
        //For all cascade member (IBookAuthor.Author) targets add direct links to all non-cascade member targets 
        // (from IBook to IAuthor)
        foreach (var cascMember in cascadeMembers) {
          var cascTarget = cascMember.ReferenceInfo.ToKey.Entity;
          foreach (var nonCascMember in nonCascadeMembers) {
            var nonCascTarget = nonCascMember.ReferenceInfo.ToKey.Entity;
            cascTarget.SccVertex.AddLink(nonCascTarget.SccVertex);
          }
        }//foreach cascMember
      }//foreach ent

      //Build SCC
      var sccCount = g.BuildScc();
      //read scc index and clear vertex fields
      foreach (var ent in _model.Entities) { 
        var v = ent.SccVertex;
        ent.TopologicalIndex = v.SccIndex;
        if (v.NonTrivialGroup)
          ent.Flags |= EntityFlags.TopologicalGroupNonTrivial;
        ent.SccVertex = null;
      }
    }

    //Builds entity.InitialValues array that will be used to initialize new entities
    private void BuildDefaultInitialValues() {
      var zero8bytes = new byte[] {0,0,0,0, 0,0,0,0}; //8 zero bytes
      foreach (var entity in _model.Entities) {
        entity.InitialColumnValues = new object[entity.PersistentValuesCount];
        foreach (var member in entity.Members)
          switch (member.Kind) {
            case MemberKind.Column:
              object dftValue; 
              if (member.Flags.IsSet(EntityMemberFlags.ForeignKey)) 
                dftValue = DBNull.Value;
              else if (member.AutoValueType == AutoType.RowVersion) {
                dftValue = zero8bytes;
              } else 
                dftValue = member.DataType.IsValueType ? Activator.CreateInstance(member.DataType) : DBNull.Value;
              member.DefaultValue = member.DefaultValue = dftValue;
              entity.InitialColumnValues[member.ValueIndex] = dftValue;
              break; 
            case MemberKind.Transient:
              member.DefaultValue = member.DeniedValue = null;
              break; 
            case MemberKind.EntityRef:
              member.DefaultValue = member.DeniedValue = DBNull.Value;
              break; 
            case MemberKind.EntityList:
              member.DefaultValue = null;
              member.DeniedValue = ReflectionHelper.CreateReadOnlyCollection(member.ChildListInfo.TargetEntity.EntityType);
              break; 
          }//switch
        
      } // foreach entity
    }

    public void PreprocessViewQueries()  {
      var views = _model.Entities.Where(e => e.Kind == EntityKind.View).ToList();
      foreach (var view in views) {
        var cmd = view.ViewDefinition.Command;
        LinqCommandAnalyzer.Analyze(_model, cmd);
        LinqCommandPreprocessor.PreprocessCommand(_model, cmd); 
      }
    } 

    public static void SetModel(EntityApp app, EntityModel model) {
      app.Model = model;
    }

    private LinqCommandInfo BuildGetCountForEntityRef(EntityReferenceInfo refInfo) {
      var child = refInfo.FromMember.Entity;
      var parent = refInfo.ToKey.Entity;
      var refMember = refInfo.FromMember;
      //build expression
      var sessionPrm = Expression.Parameter(typeof(IEntitySession), "_session");
      var parentInstance = Expression.Parameter(parent.EntityType, "_parent");
      // Build lambda for WHERE
      var cPrm = Expression.Parameter(child.EntityType, "_child");
      var parentRef = Expression.MakeMemberAccess(cPrm, refMember.ClrMemberInfo);
      var eq = Expression.Equal(parentRef, parentInstance);
      var whereLambda = Expression.Lambda(eq, cPrm);
      // 
      var genEntSet = ExpressionUtil.SessionEntitySetMethod.MakeGenericMethod(child.EntityType);
      var entSetCall = Expression.Call(sessionPrm, genEntSet);
      var genWhereMethod = ExpressionUtil.QueryableWhereMethod.MakeGenericMethod(child.EntityType);
      var whereCall = Expression.Call(genWhereMethod, entSetCall, whereLambda);
      var genCount = ExpressionUtil.QueryableCountMethod.MakeGenericMethod(child.EntityType);
      var countCall = Expression.Call(genCount, whereCall);

      var entQuery = new EntityQuery(null, child.EntityType, countCall);
      var cmd = new LinqCommand(entQuery, LinqCommandType.Select, LinqCommandKind.PrebuiltQuery, child);
      Linq.LinqCommandAnalyzer.Analyze(_model, cmd);
      Linq.LinqCommandPreprocessor.PreprocessCommand(_model, cmd);
      cmd.Info.Options |= QueryOptions.NoEntityCache;
      return cmd.Info; 
    }


  }//class

}

