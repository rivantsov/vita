using System;
using System.Reflection;

using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Model.Construction;
using Proxemity;

namespace Vita.Entities.Emit {
  public class EntityClassEmitter : IEntityClassProvider {
    DynamicAssemblyInfo _assemblyInfo; 

    public static IEntityClassProvider CreateEntityClassProvider() {
      return new EntityClassEmitter();
    }

    public void SetupEntityClasses(EntityModel model) {
      var ns = model.App.GetType().Namespace + ".Proxies";
      _assemblyInfo = _assemblyInfo ?? DynamicAssemblyInfo.Create(ns);

      var attrHandler = new AttributeHandler(addStandardAttributes: true); 
      foreach(var ent in model.Entities) {
        var className = ns + "." + ent.EntityType.Name.Substring(1); //Cut-off I
        var controller = new EntityEmitController(ent, _assemblyInfo, className, attrHandler);
        var emitter = new ProxyEmitter(controller);
        emitter.ImplementInterface(ent.EntityType);
        var classType = emitter.CreateClass();
        var factory = emitter.GetProxyFactory<Func<EntityRecord, EntityBase>>();
        ent.ClassInfo = new EntityClassInfo() { Type = classType, CreateInstance = factory };
        //assign member Clr info
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach(var member in ent.Members) {
          member.ClrClassMemberInfo = classType.GetProperty(member.MemberName, flags); 
        }
      }
    }
  }
}
