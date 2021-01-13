using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Proxemity;
using Vita.Entities.Runtime;
using Vita.Entities.Model;

namespace Vita.Entities.Model.Emit {

  public class EntityEmitController : ProxyEmitControllerBase {
    static FieldInfo _targetRef;
    static MethodInfo _recordGetValue;
    static MethodInfo _recordSetValue;

    EntityInfo _entityInfo; 

    public EntityEmitController(EntityInfo entityInfo, DynamicAssemblyInfo asm, string className, AttributeHandler attrHandler) : base(asm, className, typeof(EntityBase), attrHandler) {
      _entityInfo = entityInfo;
      if(_targetRef == null)
        InitMethodRefs(); 
    }

    public override MethodEmitInfo GetMethodEmitInfo(MethodInfo interfaceMethod, PropertyInfo parentProperty = null) {
      var propName = parentProperty.Name;
      var member = _entityInfo.GetMember(propName, throwIfNotFound: true); 
      bool isGet = interfaceMethod.Name.StartsWith("get_");
      if(isGet) {
        var args = new object[] { member.Index };
        return new MethodEmitInfo(interfaceMethod, _targetRef, _recordGetValue, args);
      } else {
        var valuePrm = interfaceMethod.GetParameters()[0];
        var args = new object[] { member.Index, valuePrm };
        return new MethodEmitInfo(interfaceMethod, _targetRef, _recordSetValue, args);
      }
    }

    private static void InitMethodRefs() {
      _targetRef = typeof(EntityBase).GetField("Record");
      _recordGetValue = typeof(EntityRecord).GetMethod("GetValue", new Type[] { typeof(int) });
      _recordSetValue = typeof(EntityRecord).GetMethod("SetValue", new Type[] { typeof(int), typeof(object) });
    }

  } //class
}
