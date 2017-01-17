using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using System.ComponentModel;

namespace Vita.Entities.Model.Construction {

  // A class in charge of building Entity classes - classes that implement entity interfaces. 
  public class EntityClassBuilder {

    public const string EntityClassesNamespace = "EntityClasses";
    public const string InstanceCreatorMethodName = "CreateInstance";
    static ConstructorInfo _baseDefaultConstructor;
    static ConstructorInfo _baseConstructor;
    static Type _baseEntityClass;
    static FieldInfo _entityBase_Record;
    static MethodInfo _entityRecord_GetValue;
    static MethodInfo _entityRecord_SetValue;

    EntityModel _model; 

    
    public void BuildEntityClasses(EntityModel model) {
      _model = model;
      //Initialize static fields
      if (_baseEntityClass == null) {
        _baseEntityClass = typeof(EntityBase);
        _baseDefaultConstructor = _baseEntityClass.GetConstructor(Type.EmptyTypes);
        _baseConstructor = _baseEntityClass.GetConstructor(new Type[] { typeof(EntityRecord) });
        _entityBase_Record = typeof(EntityBase).GetField("Record");
        _entityRecord_GetValue = typeof(EntityRecord).GetMethods().First(m => m.Name == "GetValue" && m.GetParameters()[0].ParameterType == typeof(int));
        _entityRecord_SetValue = typeof(EntityRecord).GetMethods().First(m => m.Name == "SetValue" && m.GetParameters()[0].ParameterType == typeof(int));
      }
      //Build classes
      BuildClasses(); 
    }

    private void BuildClasses() {
      var entities = _model.Entities; 
      // Step 1 - assign namespace
      _model.ClassesNamespace = "EntityClasses." + _model.App.AppName;
      _model.ClassesAssembly = new EntityClassesAssembly();
      //Step 2 - create type builder for every entity and save it in entityInfo.ClassInfo.Type property
      foreach (var entityInfo in entities)
        CreateTypeBuilder(entityInfo);
      //Step 3 - create methods for every class
      foreach (var entityInfo in entities)
        BuildEntityMembers(entityInfo);
      //Step 4 - create types
      foreach (var entityInfo in entities)
        CreateType(entityInfo);
    }

    private string GetClassName(EntityInfo entityInfo) {
      return _model.ClassesNamespace + "." + entityInfo.Area.Name + "_" + entityInfo.Name + "Class";
    }

    private void CreateTypeBuilder(EntityInfo entityInfo) {
      var interfaceType = entityInfo.EntityType;
      var typename = GetClassName(entityInfo);
      var moduleBuilder = _model.ClassesAssembly.ModuleBuilder;
      var typeBuilder =  moduleBuilder.DefineType(typename, TypeAttributes.Class | TypeAttributes.Public, _baseEntityClass, new Type[] { interfaceType });
      entityInfo.ClassInfo = new EntityClassInfo();
      entityInfo.ClassInfo.Type = typeBuilder; //save it here
    }

    private void BuildEntityMembers(EntityInfo entityInfo) {
      var typeBuilder = (TypeBuilder)entityInfo.ClassInfo.Type; 
      foreach(var member in entityInfo.Members) {
        if (member.Kind == MemberKind.EntityRef) {
          BuildEntityRefProperty(typeBuilder, member);
        } else {
          BuildRegularProperty(typeBuilder, member);
        }
      }//foreach prop
      CreateConstructorsAndFactoryMethod(entityInfo, typeBuilder);
    }//method

    private void CreateType(EntityInfo entityInfo) {
      var classInfo = entityInfo.ClassInfo; 
      var typeBuilder = (TypeBuilder)classInfo.Type;
      CloneCustomAttributes(typeBuilder, entityInfo); 
      classInfo.Type = typeBuilder.CreateType();
      //assign references to factory methods
      classInfo.CreateInstance = (EntityCreatorMethod)Delegate.CreateDelegate(typeof(EntityCreatorMethod), classInfo.Type, InstanceCreatorMethodName);
      // assign class properties
      foreach (var memberInfo in entityInfo.Members) {
        memberInfo.ClrClassMemberInfo = classInfo.Type.GetProperty(memberInfo.MemberName); 
      }
    }

    private void BuildRegularProperty(TypeBuilder typeBuilder, EntityMemberInfo member) {
      var propBuilder = typeBuilder.DefineProperty(member.MemberName, PropertyAttributes.None, member.DataType, Type.EmptyTypes);
      CreateGetter(typeBuilder, propBuilder, member.Index);
      // Note: we create setters for all "real" properties, even if interface has only getter - to work properly with LINQ, which uses setter 
      // to assign value coming from the database; 
      // Computed property - it does not need setter, but we still create it, just in case interface prop has it (by mistake), to avoid 
      //   TypeLoad exception. Note: ModelBuilder detects this and logs an error (Computed prop may not have a setter)
      CreateSetter(typeBuilder, propBuilder, member.Index);
      CloneCustomAttributes(propBuilder, member);
    }

    //Special case for entity references, because of LINQ specifics. 
    // In Xml Linq mapping, we associate constructed classes (not interfaces) with db tables.
    // (this comes from requirement that LINQ entities should be classes with default parameterless constructors,
    // so interfaces do not work there).
    // For LINQ to work properly we need to have properties (of kind EntityRef) to be of type 
    // of generated class, not the original entity interface.
    private void BuildEntityRefProperty(TypeBuilder typeBuilder, EntityMemberInfo member) {
      //1. Create private property matching interface property and implementing it explicitly
      var intfType = member.Entity.EntityType; 
      var propName = intfType.Name + "." + member.MemberName; 
      var propBuilder = typeBuilder.DefineProperty(propName, PropertyAttributes.None, member.DataType, Type.EmptyTypes);
      var getterName = intfType.Name + ".get_" + member.MemberName; 
      var getter = CreateGetter(typeBuilder, propBuilder, member.Index, getterName, false);
      //Associate the created getter method with the interface method
      var igetter = intfType.FindMethod("get_" + member.MemberName);
      typeBuilder.DefineMethodOverride(getter, igetter);
      //var canWrite = (member.Property == null || member.Property.CanWrite);
      //if (canWrite) {
        var setterName = intfType.Name + ".set_" + member.MemberName;
        var setter = CreateSetter(typeBuilder, propBuilder, member.Index, setterName, false);
        var isetter = intfType.FindMethod("set_" + member.MemberName);
        if (isetter != null)
          typeBuilder.DefineMethodOverride(setter, isetter); //associate with setter in interface
      //}
      //2. Create public property, with the same name as member name, but with type equal to the Class 
      // constructed on target entity. This is needed for proper interpretation by LINQ. 
      var classType = member.Entity.ClassInfo.Type; //TypeBuilder in fact, at this moment
      propName = member.MemberName;
      var targetEntityClass = member.ReferenceInfo.ToKey.Entity.ClassInfo.Type;
      propBuilder = typeBuilder.DefineProperty(propName, PropertyAttributes.None, targetEntityClass, Type.EmptyTypes);
      getter = CreateGetter(typeBuilder, propBuilder, member.Index);
      //if (canWrite)
        CreateSetter(typeBuilder, propBuilder, member.Index);
      CloneCustomAttributes(propBuilder, member);
    }

    private void CloneCustomAttributes(PropertyBuilder property, EntityMemberInfo member) {
      foreach(var attr in member.Attributes) {
        CustomAttributeHandler handler;
        if (!_model.App.AttributeHandlers.TryGetValue(attr.GetType(), out handler)) continue;
        var attrBuilder = handler.Clone(attr);
        if (attrBuilder != null)
          property.SetCustomAttribute(attrBuilder); 
      }
    }

    private void CloneCustomAttributes(TypeBuilder typeBuilder, EntityInfo entity) {
      foreach (var attr in entity.Attributes) {
        CustomAttributeHandler handler;
        if (!_model.App.AttributeHandlers.TryGetValue(attr.GetType(), out handler)) continue;
        var attrBuilder = handler.Clone(attr);
        if (attrBuilder != null)
          typeBuilder.SetCustomAttribute(attrBuilder);
      }
    }

    private void CreateConstructorsAndFactoryMethod(EntityInfo entityInfo, TypeBuilder typeBuilder) {
      //Create constructor with single EntityRecord parameter:     public MyEntity(EntityRecord record) : base(record) { }
      var constrAttrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName;
      var paramRecord = new Type[] { typeof(EntityRecord) };
      var entConstr = typeBuilder.DefineConstructor(constrAttrs, CallingConventions.HasThis, paramRecord);
      // IL_0000: ldarg.0
      // IL_0001: ldarg.1
      // IL_0002: call instance void Ns.EntityBase::.ctor(class Ns.EntityRecord)
      // IL_0007: ret
      var ilGen = entConstr.GetILGenerator();
      ilGen.Emit(OpCodes.Ldarg_0);
      ilGen.Emit(OpCodes.Ldarg_1);
      ilGen.Emit(OpCodes.Call, _baseConstructor);
      ilGen.Emit(OpCodes.Ret);

      //Create CreateInstance static factory method; IL:
      // IL_0000: ldarg.0
      // IL_0001: newobj instance void Ns.MyEntity::.ctor(class Ns.EntityRecord)
      // IL_0006: ret      
      var methFlags = MethodAttributes.Static | MethodAttributes.Public;
      var createInstanceBuilder = typeBuilder.DefineMethod(InstanceCreatorMethodName, methFlags, typeof(EntityBase), paramRecord);
      ilGen = createInstanceBuilder.GetILGenerator();
      ilGen.Emit(OpCodes.Ldarg_0);
      ilGen.Emit(OpCodes.Newobj, entConstr);
      ilGen.Emit(OpCodes.Ret);
    }

/* deprecated
    //IL: 
    //     L_0000: newobj instance void Vita.Framework.EntityListProxy`1<class Vita.Framework.SampleEntityImpl.ISampleEntity>::.ctor()
    //
    private void CreateListTypeAndListCreatorMethod(EntityInfo entityInfo, TypeBuilder entityTypeBuilder) {
      //Construct concrete list type from generic EntityListProxy<> type
      var listGenericType = typeof(ObservableEntityList<>); 
      entityInfo.ClassInfo.ListType = listGenericType.MakeGenericType(entityInfo.EntityType);
      var listConstr = entityInfo.ClassInfo.ListType.GetConstructor(Type.EmptyTypes); 
      //Now create static factory method
      var methFlags = MethodAttributes.Static | MethodAttributes.Public;
      var createListProxyBuilder = entityTypeBuilder.DefineMethod(ListProxyCreatorMethodName, methFlags, typeof(IList), Type.EmptyTypes);
      var ilGen = createListProxyBuilder.GetILGenerator();
      ilGen.Emit(OpCodes.Newobj, listConstr);
      ilGen.Emit(OpCodes.Ret);

    }
*/


    /* Getter IL
  IL_0000: ldarg.0
  IL_0001: ldfld ns.EntityBase::Record
  IL_0006: ldc.i4.1
  IL_0007: callvirt instance object ExperimentsApp.StaticMembers.EntityRecord::GetValue(int32)
  IL_000c: castclass [mscorlib]System.String
  IL_0011: ret
  */
    private MethodBuilder CreateGetter(TypeBuilder typeBuilder, PropertyBuilder propertyBuilder, int memberIndex, 
          string name = null, bool asPublic = true) {
      var getterName = name ?? "get_" + propertyBuilder.Name;
      var attrs = MethodAttributes.SpecialName | MethodAttributes.Virtual | MethodAttributes.HideBySig;
      if (asPublic)
        attrs |= MethodAttributes.Public;
      else
        attrs |= MethodAttributes.Private;
      var propType = propertyBuilder.PropertyType;
      var getter = typeBuilder.DefineMethod(getterName, attrs, propType, Type.EmptyTypes);
      var ilGen = getter.GetILGenerator();
      ilGen.Emit(OpCodes.Ldarg_0);
      ilGen.Emit(OpCodes.Ldfld, _entityBase_Record);
      ilGen.Emit(OpCodes.Ldc_I4, memberIndex);
      ilGen.Emit(OpCodes.Call, _entityRecord_GetValue);
      if (propType.IsValueType) // || propType.IsEnum) 
        ilGen.Emit(OpCodes.Unbox_Any, propType);
      else
        ilGen.Emit(OpCodes.Castclass, propType);
      ilGen.Emit(OpCodes.Ret);
      propertyBuilder.SetGetMethod(getter);
      return getter; 
    }

    /*  Setter IL
      IL_0000: ldarg.0
      IL_0001: ldfld Ns.EntityBase::Record
      IL_0006: ldc.i4.1
      IL_0007: ldarg.1
      IL_0008: callvirt instance void Ns.EntityRecord::SetValue(int32, object)
      IL_000d: ret
 
     */
    private MethodBuilder CreateSetter(TypeBuilder typeBuilder, PropertyBuilder propertyBuilder, int memberIndex,
          string name = null, bool asPublic = true) {
      var setterName = name ?? "set_" + propertyBuilder.Name;
      var attrs = MethodAttributes.SpecialName | MethodAttributes.Virtual | MethodAttributes.HideBySig;
      if (asPublic)
        attrs |= MethodAttributes.Public;
      else
        attrs |= MethodAttributes.Private;
      var propType = propertyBuilder.PropertyType;
      var setter = typeBuilder.DefineMethod(setterName, attrs, null, new Type[] { propType });
      var ilGen = setter.GetILGenerator();
      ilGen.Emit(OpCodes.Ldarg_0);
      ilGen.Emit(OpCodes.Ldfld, _entityBase_Record);
      ilGen.Emit(OpCodes.Ldc_I4, memberIndex);
      ilGen.Emit(OpCodes.Ldarg_1);
      if (propType.IsValueType || propType.IsEnum)
        ilGen.Emit(OpCodes.Box, propType);
      ilGen.Emit(OpCodes.Call, _entityRecord_SetValue);
      ilGen.Emit(OpCodes.Ret);
      propertyBuilder.SetSetMethod(setter);
      return setter; 
    }

  }//class

}//ns

