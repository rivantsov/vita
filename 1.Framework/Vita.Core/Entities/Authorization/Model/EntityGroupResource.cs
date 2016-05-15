using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization {

  /// <summary> An authorization resource consisting of a group of entities. </summary>
  /// <remarks>Resources are combined with actions to form permissions.</remarks> 
  public class EntityGroupResource {
    public readonly string Name; 
    public readonly List<EntityResource> Entities = new List<EntityResource>();

    public EntityGroupResource(string name) {
      Name = name; 
    }
    public EntityGroupResource(string name, params Type[] entityTypes) : this(name) {
      if (entityTypes != null)
        Add(entityTypes); 
    }
    public EntityGroupResource(string name, IEnumerable<Type> entityTypes) : this(name) {
      Add(entityTypes);
    }
    public EntityGroupResource(string name, params EntityResource[] entities)
      : this(name) {
      if (entities != null)
        Add(entities); 
    }
    public EntityGroupResource(string name, params EntityGroupResource[] entities) : this(name) {
      if (entities != null)
        Add(entities);
    }

    public void Add<EntityType>(params Expression<Func<EntityType, object>>[] propertySelectors) {
      var propNames = string.Join(",", propertySelectors.Select(ps => ReflectionHelper.GetSelectedProperty(ps)));
      this.Add(typeof(EntityType), propNames);
    }

    public void Add(Type entityType, string properties) {
      var entRes = new EntityResource(entityType, properties);
      Entities.Add(entRes);
    }

    public void Add(params Type[] entityTypes) {
      foreach (var type in entityTypes)
        Entities.Add(new EntityResource(type));
    }
    public void Add(IEnumerable<Type> entityTypes) {
      foreach (var type in entityTypes)
        Entities.Add(new EntityResource(type));
    }
    public void Add(params EntityResource[] entities) {
      Entities.AddRange(entities);
    }
    public void Add(params EntityGroupResource[] groups) {
      foreach(var g in groups)
        Entities.AddRange(g.Entities);  
    }

    bool _initialized; //to avoid multiple initializations
    public void Init(EntityModel model) {
      if (_initialized)
        return;
      _initialized = true; 
      foreach (var ent in this.Entities)
        ent.Init(model);
    }

    public override string ToString() {
      return Name; 
    }

  }

}
