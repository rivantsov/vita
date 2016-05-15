using System;
using System.Collections.Generic;
using System.Linq;

using Vita.Common;
using Vita.Entities.Model.Construction;

namespace Vita.Entities.Model {

  public enum EntityModelState {
    Created,
    EntitiesConstructed,
    Ready,
  }

  public class EntityModel  {
    public readonly EntityApp App;
    public EntityModelState ModelState { get; internal set; }

    public Dictionary<string, Type> EnumTypes = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);

    public EntityClassesAssembly ClassesAssembly; 
    internal string ClassesNamespace;

    private List<EntityCommand> _commands = new List<EntityCommand>();
    private IList<EntityInfo> _entities = new List<EntityInfo>();

    private Dictionary<Type, EntityInfo> _entitiesByType = new Dictionary<Type, EntityInfo>(); //interfaceType -> EntityInfo
    private Dictionary<string, EntityInfo> _entitiesByName = new Dictionary<string, EntityInfo>(StringComparer.InvariantCultureIgnoreCase); //name -> EntityInfo

    public EntityModel(EntityApp app) {
      App = app;
      ModelState = EntityModelState.Created; 
    }


    public ICollection<EntityInfo> Entities {
      get { return _entities; }
    }
    public IEnumerable<Type> GetAllEntityTypes() {
      return _entities.Select(e => e.EntityType);
    }

    // Remember that entities might be replaced when aggregating modules
    public void RegisterEntity(EntityInfo entityInfo) {
      //Only for final entities
      if (entityInfo.ReplacedBy == null) {
        //Register by full name and add to list of final entities
        _entitiesByName[entityInfo.FullName] = entityInfo;
        _entities.Add(entityInfo);
      }
      //For type->entity table, register final entity for original type 
      var finalEntityInfo = entityInfo.ReplacedBy ?? entityInfo;
      _entitiesByType[entityInfo.EntityType] = finalEntityInfo;
      foreach(var replType in entityInfo.ReplacesTypes)
        _entitiesByType[replType] = entityInfo; 
    }
    public void RegisterEntity(EntityInfo entityInfo, Type entityType) {
      _entitiesByType[entityType] = entityInfo;
    }

    public bool IsRegisteredEntityType(Type entityType) {
      if (!entityType.IsInterface)
        return false;
      return _entitiesByType.ContainsKey(entityType); 
    }

    public EntityInfo GetEntityInfo(Type entityType, bool throwIfNotFound = false) {
      EntityInfo entityInfo;
      if(_entitiesByType.TryGetValue(entityType, out entityInfo)) 
        return entityInfo;
      if (App.LinkedApps.Count > 0)
        foreach(var linkedApp in App.LinkedApps) {
          entityInfo = linkedApp.Model.GetEntityInfo(entityType, false);
          if(entityInfo != null)
            return entityInfo; 
        }

      if (throwIfNotFound)
        Util.Throw("Type {0} is not registered as entity.", entityType);
      return null; 
    }

    public EntityInfo GetEntityInfo(string fullName, bool throwIfNotFound = false) {
      EntityInfo entityInfo;
      if (_entitiesByName.TryGetValue(fullName, out entityInfo)) return entityInfo;
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
        return module.Sequences.FirstOrDefault(s => s.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
      //It is a fully-qualified name: schema.name
      var segms = name.Split('.');
      var schema = segms[0];
      var nm = segms[1];
      foreach (var m in this.App.Modules) {
        if (!m.Area.Name.Equals(schema, StringComparison.InvariantCultureIgnoreCase)) continue;
        var seq = m.Sequences.FirstOrDefault(s => s.Name.Equals(nm, StringComparison.InvariantCultureIgnoreCase));
        if (seq != null)
          return seq;
      }
      return null;  //not found     
    }

    #region Commands access
    public ICollection<EntityCommand> Commands {
      get {return _commands;}
    }
    
    public ICollection<EntityCommand> GetCrudCommands() {
      return _commands.Where(c => !c.Flags.IsSet(EntityCommandFlags.IsCustom)).ToList(); 
    }
    public ICollection<EntityCommand> GetCustomCommands() {
      return _commands.Where(c => c.Flags.IsSet(EntityCommandFlags.IsCustom)).ToList();
    }
    public void AddCommand(EntityCommand command) {
      _commands.Add(command); 
    }
    #endregion

  }//class

}//namespace
