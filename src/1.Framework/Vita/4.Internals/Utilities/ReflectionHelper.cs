using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Vita.Entities.Utilities {

  public static partial class ReflectionHelper {
    public const BindingFlags BindingFlagsAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Dictionary<string, Type> _typesCache = new Dictionary<string, Type>();
    private static object _lockObject = new object();



    public static bool CanAssignWithConvert(this Type type, Type fromType) {
      if(type.IsAssignableFrom(fromType))
        return true;
      if(fromType.ImplementsInterface<IConvertible>())
        return true;
      return false; 
    }
    /*
    public static Type GetLoadedType(string fullName, bool throwIfNotFound = true) {
      var type = Type.GetType(fullName);
      if (type != null)
        return type;
      lock(_lockObject) {
        if(_typesCache.TryGetValue(fullName, out type))
          return type;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach(var asm in assemblies) {
          type = asm.GetType(fullName);
          if(type != null) {
            _typesCache.Add(fullName, type);
            return type;
          }
        }// foreach asm
      }// lock
      if (!throwIfNotFound)
        Util.Throw("Type {0} not found or not loaded.", fullName);
      return null; 
    }
    */


    // Type.GetProperties does not return all properties (including inherited), only the properties declared on interface itself.
    // That's the reason for this method. 
    //TODO: add support for re-introducing properties with "new" keyword
    public static IList<PropertyInfo> GetAllProperties(this Type interfaceType) {
      //var ti = interfaceType.GetTypeInfo();
      var result = new List<PropertyInfo>();
      var props = interfaceType.GetProperties();
      result.AddRange(props);
      var interfaces = interfaceType.GetInterfaces();
      foreach (var intf in interfaces) {
        result.AddRange(intf.GetProperties());
      }
      return result;
    }

    public static MethodInfo FindMethod(this Type interfaceType, string methodName) {
      var ti = interfaceType; //.GetTypeInfo(); 
      var method = ti.GetMethod(methodName);
      if (method != null)
        return method;  
      var interfaces = ti.GetInterfaces();
      foreach (var intf in interfaces) {
        method = intf.GetMethod(methodName);
        if (method != null)
          return method; 
      }
      return null; 
    }

    public static MethodInfo FindMethod(this Type type, string methodName, int paramCount, BindingFlags? bindingFlags = null) {
      var flags = bindingFlags == null ? BindingFlagsAll : bindingFlags.Value;
      var ti = type; //.GetTypeInfo();
      var method = ti.GetMethod(methodName, flags);
      if(method != null)
        return method;
      var methods = ti.GetMethods(flags).Where(m => m.Name == methodName && m.GetParameters().Length == paramCount).ToArray();
      Util.Check(methods.Length > 0, "Method {0} not found on type {1}.", methodName, type);
      Util.Check(methods.Length < 2, "Found more than one method {0} on type {1}.", methodName, type);
      return methods[0];
    }

    public static PropertyInfo FindProperty(this Type interfaceType, string propertyName) {
      return interfaceType.GetAllProperties().FirstOrDefault(p => p.Name == propertyName); 
    }

    public static bool HasSetter(this MemberInfo member) {
      var propInfo = member as PropertyInfo;
      if(propInfo == null)
        return false;
      var setter = propInfo.GetSetMethod();
      return setter != null; 
    }

    public static bool ImplementsInterface(this Type type, Type interfaceType) {
      return interfaceType.IsAssignableFrom(type); 
    }
    public static bool ImplementsInterface<InterfaceType>(this Type type) {
      return type.ImplementsInterface(typeof(InterfaceType));
    }

    public static bool IsGenericQueryable(this Type type) {
      return (type.GetTypeInfo().IsGenericType && typeof(IQueryable).IsAssignableFrom(type));
    }

    public static bool HasAttribute<TAttr>(this MemberInfo member) where TAttr: Attribute {
      var attr = member.GetAttribute<TAttr>();
      return attr != null; 
    }

    public static T GetAttribute<T>(this MemberInfo member, bool orSubClass = false) where T: Attribute {
      return GetAttributes<T>(member, orSubClass).FirstOrDefault();
    }

    public static IList<T> GetAttributes<T>(this MemberInfo member, bool orSubClass = false) where T: Attribute {
      if(member == null)
        return new List<T>();
      var attrs = member.GetCustomAttributes(true);
      if(orSubClass)
        return attrs.Where(a => a is T).Select(a => a as T).ToList();
      else
        return attrs.Where(a => a.GetType() == typeof(T)).Select(a => a as T).ToList();
    }

    //NOTE: GetCustomAttributes does NOT return attributes from parent interfaces, even with parameter inherit:true.
    // This parameter impacts parent class, not interfaces. So we need to use our custom method to collect ALL attributes.
    public static IList<Attribute> GetAllAttributes(this Type interfaceType, bool inherit = true) {
      var result = new List<Attribute>();
      result.AddFrom(interfaceType);
      if(inherit)
        foreach(var parent in interfaceType.GetInterfaces())
          result.AddFrom(parent);
      return result; 
    }

    private static void AddFrom(this List<Attribute> toList, Type fromType) {
      var attrs = fromType.GetTypeInfo().GetCustomAttributes(true).Select(a => (Attribute)a).ToList();
      toList.AddRange(attrs); 
    }

    public static IList<Attribute> GetAllAttributes(this MemberInfo member, bool inherit = true) {
      var result = member.GetCustomAttributes(inherit).Select(a => (Attribute)a).ToList();
      return result;
    }

    public static Type[] AllIntTypes = new Type[] {
         typeof(Int32), typeof(UInt32),
         typeof(byte), typeof(sbyte),
         typeof(Int16), typeof(UInt16),
         typeof(Int64), typeof(UInt64)};

    public static bool IsInt(this Type type) {
      return AllIntTypes.Contains(type); 
    }

    public static bool IsListOrArray(this Type type) {
      if (type == typeof(string))
        return false;
      if (type.IsArray)
        return true;
      if (!type.IsGenericType) 
        return false;
      if (IsGenericList(type))
        return true; 
      // important - Linq engine relies on this check for IEnumerable (not IList)
      if (typeof(IEnumerable).IsAssignableFrom(type))
        return true; 
      return false;
    }

    public static bool IsGenericList(this Type type) {
      if (!type.GetTypeInfo().IsGenericType) return false;
      var genType = type.GetGenericTypeDefinition();
      var result = typeof(IList<>).IsAssignableFrom(genType);
      return result; 
    }
    
    
    public static bool IsAnonymousType(this Type type) {
      return type.Name.StartsWith("<"); //fast and dirty version
/* recommended version (in blogs)
      var hasCompilerGeneratedAttribute = type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Count() > 0;
      var nameContainsAnonymousType = type.FullName.Contains("AnonymousType");
      return hasCompilerGeneratedAttribute && nameContainsAnonymousType;
 */ 
    }

    internal static bool IsEntity_(this Type type) {
      var ti = type.GetTypeInfo();
      if (!ti.IsInterface)
        return false;
      var attrs = ti.GetCustomAttributes(false);
      var entAttr = attrs.FirstOrDefault(a => a is Vita.Entities.EntityAttribute);
      return entAttr != null;
    }

    public static bool IsTypeOrSubType(this Type type, object instance) {
      if(instance == null) return false;
      var t = instance.GetType();
      return t == type || t.GetTypeInfo().IsSubclassOf(type);

    }

    public static bool IsEntitySequence(this Type type) {
      if (!type.GetTypeInfo().IsGenericType || !typeof(IEnumerable).IsAssignableFrom(type))
        return false; 
      var elemType = type.GenericTypeArguments[0];
      return elemType.IsEntity_();
    }

    //not used, looks like
    public static MethodInfo FindExtensionMethod(Type extensionType, string methodName, Type[] typeArgs, Type[] paramTypes) {
      BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
      var methods = extensionType.GetMethods(flags).Where(m => m.Name == methodName).ToArray();
      if (methods == null || methods.Length == 0) return null;
      for (int i = 0; i < methods.Length; i++) {
        var method = methods[i];
        if (!TypeArgumentsMatch(method, typeArgs))
          continue;
        if (method.IsGenericMethod)
          method = method.MakeGenericMethod(typeArgs);
        if (!ArgumentsMatch(method, paramTypes))
          continue;
        return method;
      }
      return null;
    }//method

    private static bool TypeArgumentsMatch(MethodInfo method, Type[] typeArgs) {
      var argsEmpty = typeArgs == null || typeArgs.Length == 0;
      if (method.IsGenericMethod) {
        if (argsEmpty) return false;
        var argLen = method.GetGenericArguments().Length;
        return (argLen == typeArgs.Length);
      }
      //If method is not generic, typeArgs must be empty
      return argsEmpty == true;
    }

    private static bool ArgumentsMatch(MethodInfo method, Type[] argTypes) {
      var methodGenArgs = method.GetGenericArguments();
      var prms = method.GetParameters();
      var match = argTypes == null || prms.Length == argTypes.Length;
      if (!match) return false;
      //Check that arg type matches
      for (int i = 0; i < prms.Length; i++) {
        var prmType = prms[i].ParameterType;
        var argType = argTypes[i];
        if (!prmType.GetTypeInfo().IsAssignableFrom(argType.GetTypeInfo()))
          return false;
      }
      return true;
    }

    public static bool IsNullableValueType(this Type type) {
      return Nullable.GetUnderlyingType(type) != null;
    }

    public static bool IsNullable(this Type type) {
      return !type.GetTypeInfo().IsValueType || IsNullableValueType(type);
    }

    public static Type GetNullable(Type valueType) {
      return typeof(Nullable<>).MakeGenericType(valueType);
    }


    /*
    public static Type GetValueTypeFromNullable(this Type nullableValueType) {
      var isNullableValueType = nullableValueType.GetTypeInfo().IsGenericType && 
        nullableValueType.GetGenericTypeDefinition() == typeof(Nullable<>);
      if (!isNullableValueType) return nullableValueType;
      return nullableValueType.GetUnderlyingStorageType();
    }
    */
    public static bool IsNullableOf(this Type nullableType, Type type) {
      if (!type.GetTypeInfo().IsValueType)
        return false;
      var underType = Nullable.GetUnderlyingType(nullableType);
      return Nullable.GetUnderlyingType(nullableType) == type;
    }

    public static bool IsListContains(this MethodInfo method) {
      if (method.Name != "Contains") 
        return false;
      var declType = method.DeclaringType; 
      if (declType == typeof(string))
        return false; 
      if (declType == typeof(Queryable) || declType == typeof(Enumerable))
        return true; 
      // it might be List<T> method
      if (typeof(System.Collections.IList).IsAssignableFrom(declType))
        return true;
      return false; 
    }

    public static Func<IList> GetCompiledGenericListCreator(Type elementType) {
      var genMethod = typeof(ReflectionHelper) //.GetTypeInfo()
           .GetMethod(nameof(CreateList), BindingFlags.Static | BindingFlags.NonPublic);
      var method = genMethod.MakeGenericMethod(elementType);
      var func = (Func<IList>)  method.CreateDelegate(typeof(Func<IList>));
      return func;
    }
    //used by previous method
    private static IList CreateList<T>() {
      return new List<T>();
    }

    public static object GetMemberValue(this MemberInfo member, object obj) {
      switch(member) {
        case PropertyInfo pi:
          return pi.GetValue(obj);
        case FieldInfo fi:
          return fi.GetValue(obj);
      }
      return null; 
    }

    public static Type GetMemberReturnType(this MemberInfo member) {
      switch(member) {
        case PropertyInfo pi:
          return pi.PropertyType;
        case FieldInfo fi:
          return fi.FieldType;
        case MethodInfo mi:
          return mi.ReturnType;
        case ConstructorInfo ci:
          return null; 
      }
      Util.Throw("Invalid argument for GetMemberType: {0}", member);
      return null;
    }

    public static bool IsStaticMember(this MemberInfo member) {
      switch(member) {
        case PropertyInfo pi:
          var meth = pi.GetMethod ?? pi.SetMethod;
          return meth.IsStatic;
        case FieldInfo fi:
          return fi.IsStatic;
        case MethodInfo mi:
          return mi.IsStatic;
      }
      return false;
    }

    public static bool IsDbPrimitive(this Type type) {
      if (type.IsNullableValueType())
        type = Nullable.GetUnderlyingType(type);
      return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(Decimal)
        || type == typeof(DateTime) || type == typeof(DateTimeOffset);
    }
    public static bool IsValueTypeOrString(this Type type) {
      return type.GetTypeInfo().IsValueType || type == typeof(string);
    }

    //not used! - replaced by IsListOfDbPrimitive
    public static bool IsSequenceOfDbPrimitive(this Type type) {
      var isGenSeq = type.GetTypeInfo().IsGenericType && 
        typeof(IEnumerable).IsAssignableFrom(type);
      if(!isGenSeq)
        return false;
      var elemType = type.GenericTypeArguments[0];
      return elemType.IsDbPrimitive();
    }

    public static bool IsListOfDbPrimitive(this Type type) {
      Type elemType;
      return IsListOfDbPrimitive(type, out elemType);      
    }

    public static bool IsListOfDbPrimitive(this Type type, out Type elemType) {
      elemType = null;
      if (type.IsListOrArray()) {
        if(type.GetTypeInfo().IsGenericType)
          elemType = type.GenericTypeArguments[0];
        else if(type.IsArray)
          elemType = type.GetElementType();
        if(elemType != null && elemType.IsDbPrimitive())
          return true;
      }
      return false;
    }


    // For properties that are lists of other entities (book.Authors), when access to property is denied, 
    // we want to return an empty readonly list (rather than null) 
    internal static object CreateReadOnlyCollection(Type type) {
      var genListType = typeof(List<>).MakeGenericType(type);
      var emptyList = Activator.CreateInstance(genListType);
      var collType = typeof(ReadOnlyCollection<>);
      var genCollType = collType.MakeGenericType(type);
      var coll = Activator.CreateInstance(genCollType, emptyList);
      return coll;
    } //method

    public static object GetDefaultValue(Type type) {
       return type.GetTypeInfo().IsValueType ? Activator.CreateInstance(type) : null;
    }

    public static string GetDisplayName(this Type type) {
      var ti = type; //.GetTypeInfo();
      if (type.IsNullableValueType())
        return ti.GetGenericArguments()[0].Name + "?";
      if (ti.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
        return "IList<" + ti.GetGenericArguments()[0].Name + ">";
      switch (type.Name) {
        case "String": return "string";
        case "Int32": return "int";
        case "Int64": return "long";
      }
      return type.Name;
    }

  }//class
}//namespace
