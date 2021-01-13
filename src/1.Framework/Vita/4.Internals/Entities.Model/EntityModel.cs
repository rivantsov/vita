using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Vita.Entities.Model {


  /// <summary>Encodes model state values during model construction.</summary>
  public enum EntityModelState {
    /// <summary>Entities and Members are constructed. No keys, attributes are not processed.</summary>
    Draft,
    /// <summary>Entities, Members, Keys are constructed. Attributes are applied. References and lists are implemented.</summary>
    Constructed,
    /// <summary>Model construction completed.</summary>
    Completed,
  }

  public class EntityModel  {
    public readonly EntityApp App;
    public EntityModelState ModelState { get; internal set; }

    public Dictionary<string, Type> EnumTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

    public IList<EntityInfo> Entities = new List<EntityInfo>();
    //interfaceType -> EntityInfo; also contains entries for replaced entities (oldType => newEnt)
    public Dictionary<Type, EntityInfo> EntitiesByType = new Dictionary<Type, EntityInfo>(); 
    public Dictionary<string, EntityInfo> EntitiesByName = new Dictionary<string, EntityInfo>(StringComparer.OrdinalIgnoreCase); //name -> EntityInfo
    public IList<EntityInfo> ReplacedEntities = new List<EntityInfo>();

    internal EntityInfo NullEntityInfo;

    public EntityModel(EntityApp app) {
      App = app;
      App.Model = this;
      NullEntityInfo = new EntityInfo(typeof(INullEntity));
    }


    public IEnumerable<Type> GetAllEntityTypes() {
      return Entities.Select(e => e.EntityType);
    }

    // Remember that entities might be replaced when aggregating modules
    public void RegisterEntity(EntityInfo entityInfo) {
      //Only for final entities
      if (entityInfo.ReplacedBy == null) {
        //Register by full name and add to list of final entities
        EntitiesByName[entityInfo.FullName] = entityInfo;
        Entities.Add(entityInfo);
      }
      //For type->entity table, register final entity for original type 
      var finalEntityInfo = entityInfo.ReplacedBy ?? entityInfo;
      EntitiesByType[entityInfo.EntityType] = finalEntityInfo;
      foreach(var replType in entityInfo.ReplacesTypes)
        EntitiesByType[replType] = entityInfo; 
    }

    public void RegisterEntity(EntityInfo entityInfo, Type entityType) {
      EntitiesByType[entityType] = entityInfo;
    }

    public bool IsRegisteredEntityType(Type entityType) {
      if (!entityType.GetTypeInfo().IsInterface)
        return false;
      return EntitiesByType.ContainsKey(entityType); 
    }

    internal void RebuildModelEntitySets() {
      var oldList = Entities;
      Entities = new List<EntityInfo>();
      EntitiesByType = new Dictionary<Type, EntityInfo>();
      EntitiesByName = new Dictionary<string, EntityInfo>();
      ReplacedEntities = new List<EntityInfo>();
      foreach(var ent in oldList) {
        EntitiesByName[ent.FullName] = ent;
        if(ent.ReplacedBy == null) {
          Entities.Add(ent);
          EntitiesByType[ent.EntityType] = ent;
          foreach(var replType in ent.ReplacesTypes)
            EntitiesByType[replType] = ent;
          continue;
        } else
          ReplacedEntities.Add(ent);
      }
    }


    public EntityInfo GetEntityInfo(Type entityType, bool throwIfNotFound = false) {
      EntityInfo entityInfo;
      if(EntitiesByType.TryGetValue(entityType, out entityInfo)) 
        return entityInfo;
      if(entityType == typeof(INullEntity))
        return NullEntityInfo; 
      if (throwIfNotFound)
        Util.Throw("Type {0} is not registered as entity.", entityType);
      return null; 
    }

    public EntityInfo GetEntityInfo(string fullName, bool throwIfNotFound = false) {
      EntityInfo entityInfo;
      if (EntitiesByName.TryGetValue(fullName, out entityInfo)) return entityInfo;
      if (throwIfNotFound)
        Util.Throw("Type {0} is not registered as entity.", fullName);
      return null;
    }

    public Type GetEntityClass(Type interfaceType) {
      var entInfo = GetEntityInfo(interfaceType, true);
      return entInfo.ClassInfo.Type;
    }

    public void AddEnumType(Type type) {
      EnumTypes.Add(type.FullName.ToLowerInvariant(), type);
    }

    public Type FindEnumType(string fullName) {
      Type result;
      if (EnumTypes.TryGetValue(fullName, out result)) return result; 
      //try to find by short type name
      foreach (var de in EnumTypes)
        if (string.Compare(de.Value.Name, fullName, ignoreCase: true) == 0) return de.Value;
      //Otherwise return null
      return null;
    }

    public SequenceDefinition FindSequence(string name, EntityModule module = null) {
      //If it is a simple name, find in the sequences defined in the module
      if (module != null && !name.Contains('.')) 
        return module.Sequences.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
      //It is a fully-qualified name: schema.name
      var segms = name.Split('.');
      string seqName;
      string schema;
      switch(segms.Length) {
        case 1:
          seqName = segms[0]; 
          foreach(var m in this.App.Modules) {
            var seq = m.Sequences.FirstOrDefault(s => s.Name.Equals(seqName, StringComparison.OrdinalIgnoreCase));
            if(seq != null)
              return seq;
          }
          return null;
        case 2:
          schema = segms[0];
          seqName = segms[1];
          foreach(var m in this.App.Modules) {
            if(!m.Area.Name.Equals(schema, StringComparison.OrdinalIgnoreCase))
              continue;
            var seq = m.Sequences.FirstOrDefault(s => s.Name.Equals(seqName, StringComparison.OrdinalIgnoreCase));
            if(seq != null)
              return seq;
          }
          return null;
        default:
          Util.Throw("Invalid sequence name: {0}", name);
          return null; 
      }
    }

  }//class

}//namespace
