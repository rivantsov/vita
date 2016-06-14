using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Vita.Common {

  public static class ReflectionHelper {

    private static Dictionary<string, Type> _typesCache = new Dictionary<string, Type>();
    private static object _lockObject = new object();


    public static bool CanAssignWithConvert(this Type type, Type fromType) {
      if(type.IsAssignableFrom(fromType))
        return true;
      if(fromType.ImplementsInterface<IConvertible>())
        return true;
      return false; 
    }

    public static Type GetLoadedType(string fullName, bool throwIfNotFound = true) {
      var type = Type.GetType(fullName);
      if (type != null)
        return type;
      lock(_lockObject)
        if (_typesCache.TryGetValue(fullName, out type))
          return type; 
      var assemblies = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var asm in assemblies) {
        type = asm.GetType(fullName);
        if (type != null) {
          lock(_lockObject)
            _typesCache.Add(fullName, type);
          return type; 
        }
      }
      if (!throwIfNotFound)
        Util.Throw("Type {0} not found or not loaded.", fullName);
      return null; 
    }

    // Type.GetProperties does not return all properties (including inherited), only the properties declared on interface itself.
    // That's the reason for this method. 
    //TODO: add support for re-introducing properties with "new" keyword
    public static IList<PropertyInfo> GetAllProperties(this Type interfaceType) {
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
      var method = interfaceType.GetMethod(methodName);
      if (method != null)
        return method;  
      var interfaces = interfaceType.GetInterfaces();
      foreach (var intf in interfaces) {
        method = intf.GetMethod(methodName);
        if (method != null)
          return method; 
      }
      return null; 
    }

    public static PropertyInfo FindProperty(this Type interfaceType, string propertyName) {
      return interfaceType.GetAllProperties().FirstOrDefault(p => p.Name == propertyName); 
    }

    public static bool ImplementsInterface(this Type type, Type interfaceType) {
      return interfaceType.IsAssignableFrom(type); 
    }
    public static bool ImplementsInterface<InterfaceType>(this Type type) {
      return type.ImplementsInterface(typeof(InterfaceType));
    }

    public static bool IsGenericQueryable(this Type type) {
      return (type.IsGenericType && typeof(IQueryable).IsAssignableFrom(type));
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
    public static IEnumerable<Attribute> GetAllAttributes(this Type interfaceType, bool inherit = true) {
      var result = new List<Attribute>();
      AddAttributes(interfaceType, result); 
      if (inherit)
        foreach(var parent in interfaceType.GetInterfaces())
          AddAttributes(parent, result);
      return result; 
    }
    private static void AddAttributes(Type fromType, IList<Attribute> toList) {
      var objs = fromType.GetCustomAttributes(true);
      foreach (Attribute attr in objs)
        toList.Add(attr);
    }

    public static bool HasAttribute<T>(this MemberInfo memberOrType) where T: Attribute {
      return GetAttribute<T>(memberOrType) != null; 
    }

    public static bool IsInt(this Type type) {
      if(type.IsEnum)
        return false; 
      var typeCode = Type.GetTypeCode(type);
      switch (typeCode) {
        case TypeCode.Byte:
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.UInt16:
        case TypeCode.Int32:
        case TypeCode.UInt32:
        case TypeCode.Int64:
        case TypeCode.UInt64:
          return true;
        default:
          return false;
      }
    }

    public static bool IsListOrArray(this Type type) {
      if (type == typeof(string))
        return false;
      if (type.IsArray)
        return true; 
      if (!type.IsGenericType) return false;
      if (typeof(ICollection).IsAssignableFrom(type))
        return true; 
      var genType = type.GetGenericTypeDefinition();
      if (genType == typeof(IList<>) || genType == typeof(List<>) || genType == typeof(ICollection<>))
        return true;
      if (typeof(IEnumerable).IsAssignableFrom(type))
        return true; 
      return false;
    }

    public static bool IsGenericList(this Type type) {
      if (!type.IsGenericType) return false; 
      var genType = type.GetGenericTypeDefinition();
      var result = typeof(IList<>).IsAssignableFrom(genType);
      return result; 
    }
    public static bool IsGenericCollection(this Type type) {
      if (!type.IsGenericType) return false;
      var genType = type.GetGenericTypeDefinition();
      var result = typeof(ICollection<>).IsAssignableFrom(genType);
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
      if (!type.IsInterface)
        return false;
      var attrs = type.GetCustomAttributes(false);
      var entAttr = attrs.FirstOrDefault(a => a is Vita.Entities.EntityAttribute);
      return entAttr != null;
    }

    public static bool IsTypeOrSubType(this Type type, object instance) {
      if(instance == null) return false;
      var t = instance.GetType();
      return t == type || t.IsSubclassOf(type);

    }

    public static bool IsEntitySequence(this Type type) {
      if (!type.IsGenericType || !typeof(IEnumerable).IsAssignableFrom(type))
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
        if (!prmType.IsAssignableFrom(argType))
          return false;
      }
      return true;
    }

    public static bool IsNullableValueType(this Type type) {
      return Nullable.GetUnderlyingType(type) != null;
    }

    public static bool IsNullable(this Type type) {
      return !type.IsValueType || IsNullableValueType(type);
    }

    public static Type GetNullable(Type valueType) {
      return typeof(Nullable<>).MakeGenericType(valueType);
    }

    public static Type GetValueTypeFromNullable(this Type nullableValueType) {
      var isNullableValueType = nullableValueType.IsGenericType && nullableValueType.GetGenericTypeDefinition() == typeof(Nullable<>);
      if (!isNullableValueType) return nullableValueType;
      return nullableValueType.GetUnderlyingType();
    }
    public static Type GetUnderlyingType(this Type t) {
      return Nullable.GetUnderlyingType(t);
    }
    public static bool IsNullableOf(this Type nullableType, Type type) {
      if (!type.IsValueType)
        return false;
      var underType = Nullable.GetUnderlyingType(nullableType);
      return Nullable.GetUnderlyingType(nullableType) == type;
    }

    public static void CheckIsQueryable(Expression expression) {
      var type = expression.Type;
      Util.Check(type.IsGenericType, "Invalid query expression type ({0}) - must be generic type. ", type);
      var genericType = type.GetGenericTypeDefinition();
      Util.Check(genericType == typeof(IQueryable<>) || genericType == typeof(IOrderedQueryable<>),
                       "Invalid query expression type ({0}) - must be IQueryable<> or IOrderedQueryable<>. ", type);
    }

    public static string GetSelectedProperty<TArg>(Expression<Func<TArg, object>> memberSelector) {
      var errTemplate = "Invalid member selector expression: {0}. Must be a single property selector.";
      var target = memberSelector.Body;
      // we might have conversion expression here for value types - member access is under it
      if (target.NodeType == ExpressionType.Convert) {
        var unExpr = target as System.Linq.Expressions.UnaryExpression;
        target = unExpr.Operand;
      }
      var memberAccess = target as MemberExpression;
      Util.Check(memberAccess != null, errTemplate, memberSelector);
      return memberAccess.Member.Name;
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
      var genMethod = typeof(ReflectionHelper).GetMethod("CreateList", BindingFlags.Static | BindingFlags.NonPublic);
      var method = genMethod.MakeGenericMethod(elementType);
      var func = (Func<IList>)Delegate.CreateDelegate(typeof(Func<IList>), method);
      return func;
    }
    //used by previous method
    private static IList CreateList<T>() {
      return new List<T>();
    }

    public static object GetMemberValue(this MemberInfo memberInfo, object o) {
      switch (memberInfo.MemberType) {
        case MemberTypes.Field:
          return ((FieldInfo)memberInfo).GetValue(o);
        case MemberTypes.Property:
          return ((PropertyInfo)memberInfo).GetValue(o);
        default:
          Util.Throw("Invalid argument for GetMemberValue {0} - should be field or property.", memberInfo);
          return null;
      }
    }

    public static Type GetMemberType(this MemberInfo memberInfo) {
      switch (memberInfo.MemberType) {
        case MemberTypes.Field:
          return ((FieldInfo)memberInfo).FieldType;
        case MemberTypes.Property:
          return ((PropertyInfo)memberInfo).PropertyType;
        case MemberTypes.Method:
          return ((MethodInfo)memberInfo).ReturnType;
        case MemberTypes.Constructor:
          return null;
        case MemberTypes.TypeInfo:
          return (Type)memberInfo;
        default:
          Util.Throw("Invalid argument for GetMemberType: {0}", memberInfo);
          return null;
      }
    }

    public static bool IsStaticMember(this MemberInfo memberInfo) {
      switch (memberInfo.MemberType) {
        case MemberTypes.Field:
          return ((FieldInfo)memberInfo).IsStatic;
        case MemberTypes.Method:
          return ((MethodInfo)memberInfo).IsStatic;
        case MemberTypes.Property:
          var prop = (PropertyInfo)memberInfo;
          var meth = prop.GetMethod ?? prop.SetMethod;
          return meth.IsStatic;
        default:
          return false;
      }
    }

    public static bool IsDbPrimitive(this Type type) {
      if (type.IsNullableValueType())
        type = Nullable.GetUnderlyingType(type);
      return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(Decimal)
        || type == typeof(DateTime) || type == typeof(DateTimeOffset);
    }
    public static bool IsValueTypeOrString(this Type type) {
      return type.IsValueType || type == typeof(string);
    }

    //not used! - replaced by IsListOfDbPrimitive
    public static bool IsSequenceOfDbPrimitive(this Type type) {
      var isGenSeq = type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type);
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
        if(type.IsGenericType)
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

    public static TEnum ParseEnum<TEnum>(string value) {
      if(string.IsNullOrWhiteSpace(value))
        return default(TEnum);
      try {
        var result = (TEnum)Enum.Parse(typeof(TEnum), value, ignoreCase: true);
        return result;
      } catch(Exception ex) {
        Util.Throw("Failed to parse {0} enum value '{1}', error: {2}", typeof(TEnum), value, ex.Message);
        return default(TEnum);//never happens
      }
    }

    public static object GetDefaultValue(Type type) {
       return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    public static string GetDisplayName(this Type type) {
      if (type.IsNullableValueType())
        return type.GetGenericArguments()[0].Name + "?";
      if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
        return "IList<" + type.GetGenericArguments()[0].Name + ">";
      switch (type.Name) {
        case "String": return "string";
        case "Int32": return "int";
        case "Int64": return "long";
      }
      return type.Name;
    }


  }//class
}//namespace
