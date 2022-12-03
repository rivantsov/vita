using System;
using System.Collections;
using System.Reflection;
using Vita.Entities.Runtime;

namespace Vita.Entities.Model {

  public static class EntityEnumExtensions {

    public static bool IsEntity(this EntityModel model, Type type) {
      return model.IsRegisteredEntityType(type); 
    }

    public static bool IsEntitySequence(this EntityModel model, Type type) {
      if (!type.GetTypeInfo().IsGenericType || !typeof(IEnumerable).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
        return false;
      var elemType = type.GenericTypeArguments[0];
      return model.IsEntity(elemType);
    }

    public static EntityKey CreatePrimaryKeyInstance(this EntityInfo entity, object primaryKeyValue) {
      var pkType = primaryKeyValue.GetType();
      switch(primaryKeyValue) {
        case EntityKey key:
          return key;
        case object[] arr:
          return EntityKey.Create(entity.PrimaryKey, arr);
        default:
          return EntityKey.Create(entity.PrimaryKey, new object[] { primaryKeyValue });
      }
    }

    public static string GetKeyRefForError(this EntityKeyInfo key) {
      var keyRef = key.Entity.Name;
      if (key.OwnerMember != null)
        keyRef += $".{key.OwnerMember.MemberName}";
      keyRef += $"/{key.KeyType}";
      return keyRef;
    }


    public static bool IsSet(this EntityFlags flags, EntityFlags flag) {
      return (flags & flag) != 0;
    }
    public static bool IsSet(this EntityMemberFlags flags, EntityMemberFlags flag) {
      return (flags & flag) != 0;
    }
    public static bool IsSet(this KeyType flags, KeyType flag) {
      return (flags & flag) != 0;
    }
    public static KeyType SetFlag(this KeyType flags, KeyType flag, bool onOff) {
      return onOff ? (flags | flag) : (flags & ~flag); 
    }

    public static bool IsSet(this LoadFlags flags, LoadFlags flag) {
      return (flags & flag) != 0;
    }
  }//class


}
