using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Web;

namespace Vita.Entities.Model {

  public static class EntityEnumExtensions {

    public static bool IsEntity(this EntityModel model, Type type) {
      return model.IsRegisteredEntityType(type); 
    }

    public static bool IsEntitySequence(this EntityModel model, Type type) {
      if (!type.IsGenericType || !typeof(IEnumerable).IsAssignableFrom(type))
        return false;
      var elemType = type.GenericTypeArguments[0];
      return model.IsEntity(elemType);
    }
    

    public static bool IsSet(this LoadFlags flags, LoadFlags flag) {
      return (flags & flag) != 0;
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
    public static bool IsSet(this EntityCommandFlags flags, EntityCommandFlags flag) {
      return (flags & flag) != 0;
    }
    public static bool IsSelect(this EntityCommandKind kind) {
      switch(kind) {
        case EntityCommandKind.SelectAll: case EntityCommandKind.SelectAllPaged: 
        case EntityCommandKind.SelectByKey: case EntityCommandKind.SelectByKeyManyToMany:
          return true; 
        default:
          return false; 
      }
    }
  }//class


}
