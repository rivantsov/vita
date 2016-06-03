using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Common {
  public static class ConvertHelper {
    public static T FromString<T>(string value) {
      var type = typeof(T);
      if(type == typeof(string))
        return (T)(object)value;
      return (T)ChangeType(value, type); 
    }

    public static string ValueToString(object value) {
      if(value == null)
        return string.Empty;
      var type = value.GetType();
      if(type == typeof(string))
        return (string)value;
      if(type == typeof(DateTime))
        return ((DateTime)value).ToString("o");
      return value.ToString(); 
    }

    public static object ChangeType(object value, Type type) {
      if(value == null)
        return null;
      var valueType = value.GetType();
      if (valueType == type)
        return value; //it might happen for Nullable<T> -> T 
      //specific cases
      if(type == typeof(string))
        return value.ToString();
      if(valueType == typeof(string)) {
        var strValue = (string)value;
        if(type == typeof(Guid)) {
          Guid g;
          Util.Check(Guid.TryParse(strValue, out g), "Invalid Guid format: {0}", value);
          return g; 
        }
        if(type.IsInt()) {
          int i;
          Util.Check(int.TryParse(strValue, out i), "Invalid integer: {0}", value);
          return Convert.ChangeType(i, type);
        }
        if(type == typeof(Single) || type == typeof(Double)) {
          double d;
          Util.Check(double.TryParse(strValue, out d), "Invalid float: {0}", value);
          return Convert.ChangeType(d, type);
        }
      }
      //else try default Convert
      return Convert.ChangeType(value, type); 
    }

    public static TEnum[] ParseEnumArray<TEnum>(string value, char separator = ',') where TEnum : struct {
      if(string.IsNullOrWhiteSpace(value))
        return null;
      var type = typeof(TEnum);
      Util.Check(type.IsEnum, "Type {0} is not enum.", type);
      var strArr = value.SplitNames(separator);
      var arr = new TEnum[strArr.Length];
      for(int i = 0; i < arr.Length; i++) {
        TEnum e;
        if(Enum.TryParse<TEnum>(strArr[i], out e))
          arr[i] = e;
      }
      return arr;
    }

    public static DateTime? TryParseDateTime(string value) {
      if(string.IsNullOrWhiteSpace(value))
        return null;
      DateTime dt;
      if(DateTime.TryParse(value, out dt))
        return dt;
      return null;
    }

    public static List<string> FlagsToStringList<TEnum>(TEnum value) where TEnum : struct {
      Util.Check(typeof(TEnum).IsEnum, "Expected enum type");
      return value.ToString().SplitNames(',').ToList();
    }

    public static IEnumerable ConvertEnumerable(IEnumerable source, Type resultType) {
      if (source.GetType() == resultType)
        return source;
      Util.Check(resultType.IsGenericType, "Invalid result type: {0}. Expected generic IEnumerable<T> type.", resultType);
      var resultElemType = resultType.GetGenericArguments()[0];
      var sourceElemType = source.GetType().GetGenericArguments()[0];
      // Very special case: IEnumerable<T> -> IEnumerable<T?>
      if (resultElemType.IsNullableOf(sourceElemType)) {
        _convertToEnumerableOfNullablesMethod = _convertToEnumerableOfNullablesMethod ??
            typeof(ConvertHelper).GetMethod("ConvertToEnumerableOfNullables", BindingFlags.Static | BindingFlags.NonPublic);
        var genMethod = _convertToEnumerableOfNullablesMethod.MakeGenericMethod(sourceElemType);
        var result = genMethod.Invoke(null, new [] { source});
        return result as IEnumerable; 
      }
      //General case
      var listType = typeof(List<>).MakeGenericType(resultElemType);
      var list = Activator.CreateInstance(listType) as IList;
      foreach (var obj in source) {
        var value = obj;
        if (obj.GetType() != resultElemType)
          value = ConvertHelper.ChangeType(obj, resultElemType);
        list.Add(value);
      }
      return list;
    }

    static MethodInfo _convertToEnumerableOfNullablesMethod;
    private static IEnumerable<T?> ConvertToEnumerableOfNullables<T>(IEnumerable<T> source) where T: struct {
      return source.Select(v => new Nullable<T>(v)).ToList();
    }
  }
}
