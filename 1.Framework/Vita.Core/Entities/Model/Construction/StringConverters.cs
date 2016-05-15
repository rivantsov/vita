using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Model.Construction {
  using Binary = Vita.Common.Binary; 

  public static class StringConverters {

    public static void AssignStringConverters(EntityMemberInfo member) {
      member.ValueFromStringRef = ObjectFromString; //default
      member.ValueToStringRef = ObjectToString;
      var isNullableValueType = member.DataType.IsNullableValueType(); 
      var type = member.DataType;
      if (isNullableValueType)
        type = type.GetGenericArguments()[0];
      if (type.IsEnum) {
        member.ValueToStringRef = EnumToString;
        member.ValueFromStringRef = EnumFromString;
      } else if (type == typeof(Guid)) {
        member.ValueFromStringRef = GuidFromString;
      } else if (type == typeof(DateTimeOffset)) {
        member.ValueFromStringRef = DateTimeOffsetFromString;
      } else if (type == typeof(DateTime)) {
        member.ValueToStringRef = DateTimeToString;
        member.ValueFromStringRef = DateTimeFromString;
      } else if (type == typeof(string)) {
        member.ValueToStringRef = StringToString;
        member.ValueFromStringRef = StringFromString;
      } else if (type == typeof(Binary)) {
        member.ValueToStringRef = BinaryToString;
        member.ValueFromStringRef = BinaryFromString;
      } else if (type == typeof(byte[])) {
        member.ValueToStringRef = BytesToString;
        member.ValueFromStringRef = BytesFromString;
      } else if (isNullableValueType) {
        member.ValueFromStringRef = NullableFromString;
      }
    }// method

    public static string StringToString(EntityMemberInfo member, object value) {
      return value == null || value == DBNull.Value ? string.Empty : (string)value;
    }

    public static object StringFromString(EntityMemberInfo member, string value) {
      return value;
    }
    public static string ObjectToString(EntityMemberInfo member, object value) {
      return value == null ? string.Empty : value.ToString();
    }
    public static object ObjectFromString(EntityMemberInfo member, string value) {
      return string.IsNullOrEmpty(value)? DBNull.Value : Convert.ChangeType(value, member.DataType);
    }
    public static string DateTimeToString(EntityMemberInfo member, object value) {
      if (value == null)
        return string.Empty;
      var dt = (DateTime)value;
      return dt.ToString("O"); //round-trip specifier
    }
    public static object DateTimeFromString(EntityMemberInfo member, string value) {
      if (string.IsNullOrEmpty(value))
        return DBNull.Value;
      var dtStyle = member.Flags.IsSet(EntityMemberFlags.Utc) ? DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal : DateTimeStyles.AssumeLocal;
      var result = DateTime.Parse(value, CultureInfo.InvariantCulture, dtStyle);
      return result;
    }

    public static object DateTimeOffsetFromString(EntityMemberInfo member, string value) {
      if(string.IsNullOrEmpty(value))
        return DBNull.Value;
      var result = DateTimeOffset.Parse(value);
      return result; 
    }
    public static string BytesToString(EntityMemberInfo member, object value) {
      if (value == null)
        return string.Empty;
      return Convert.ToBase64String((byte[])value);
    }
    public static object BytesFromString(EntityMemberInfo member, string value) {
      if (string.IsNullOrEmpty(value))
        return DBNull.Value;
      return Convert.FromBase64String(value);
    }
    public static string BinaryToString(EntityMemberInfo member, object value) {
      if (value == null)
        return string.Empty;
      var bv = (Binary)value; 
      return Convert.ToBase64String(bv.ToArray());
    }
    public static object BinaryFromString(EntityMemberInfo member, string value) {
      if (string.IsNullOrEmpty(value))
        return DBNull.Value;
      var bytes =  Convert.FromBase64String(value);
      return new Binary(bytes); 
    }
    public static object NullableFromString(EntityMemberInfo member, string value) {
      if (string.IsNullOrEmpty(value))
        return DBNull.Value;
      var targetType = member.DataType.GetGenericArguments()[0];
      return Convert.ChangeType(value, targetType);
    }
    public static object GuidFromString(EntityMemberInfo member, string value) {
      if (string.IsNullOrEmpty(value))
        return DBNull.Value;// Guid.Empty;
      return Guid.Parse(value);
    }
    // Some enum values after loading from database maybe represented as integers. If we use value.ToString(), it will result in integer number -
    // not what we want for REST serialized xml. We need to convert them into meaningful names. 
    public static string EnumToString(EntityMemberInfo member, object value) {
      if (value == null) return string.Empty;
      var t = value.GetType();
      if (t.IsEnum)
        return value.ToString();
      value = Enum.ToObject(member.DataType, value);
      var asString = value.ToString();
      return asString;
    }

    public static object EnumFromString(EntityMemberInfo member, string value) {
      if (string.IsNullOrEmpty(value))
        return 0;
      return Enum.Parse(member.DataType, value, ignoreCase: true);
    }
  
  }
}
