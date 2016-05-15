using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization.Runtime {

  /// <summary> A container for detailed access rights information for an entity (instance or type-level) for a given user. </summary>
  public class UserRecordPermission {
    public AccessType AccessTypes; //insert, delete go here
    public OperationPermission Peek;
    public OperationPermission ReadStrict;
    public OperationPermission UpdateStrict;

    public static readonly UserRecordPermission AllowNone = new UserRecordPermission();
    public static readonly UserRecordPermission AllowAll = new UserRecordPermission(AccessType.CRUD);
    public static readonly UserRecordPermission AllowPeekAll = new UserRecordPermission(AccessType.Peek);
    public static readonly UserRecordPermission AllowReadAll = new UserRecordPermission(AccessType.Read);

    public UserRecordPermission() {
      AccessTypes = AccessType.None;
      Peek = new OperationPermission(AccessType.Peek, false);
      ReadStrict = new OperationPermission(AccessType.ReadStrict, false);
      UpdateStrict = new OperationPermission(AccessType.UpdateStrict, false);
    }

    public UserRecordPermission(AccessType accessTypes, EntityMemberMask memberMask = null) {
      Peek = new OperationPermission(AccessType.Peek, accessTypes.IsSet(AccessType.Peek), memberMask);
      ReadStrict = new OperationPermission(AccessType.ReadStrict, accessTypes.IsSet(AccessType.ReadStrict), memberMask);
      UpdateStrict = new OperationPermission(AccessType.UpdateStrict, accessTypes.IsSet(AccessType.UpdateStrict), memberMask);
      AccessTypes = accessTypes; 
    }

    public override string ToString() {
      var result = AccessTypes.ToString();
      if (Peek.Mask != null || ReadStrict.Mask != null || UpdateStrict.Mask != null)
        result += "/Masked";
      return "(" + result + ")"; 
    }
    public void Merge(UserRecordPermission other) {
      Peek.Merge(other.Peek);
      ReadStrict.Merge(other.ReadStrict);
      UpdateStrict.Merge(other.UpdateStrict);
      AccessTypes |= other.AccessTypes;
    }

    public static UserRecordPermission Create(EntityInfo entity, string properties, AccessType accessType) {
      if (string.IsNullOrWhiteSpace(properties)) {
        if (accessType.IsSet(AccessType.Update))
          return UserRecordPermission.AllowAll;
        if (accessType.IsSet(AccessType.ReadStrict))
          return UserRecordPermission.AllowReadAll;
        if (accessType.IsSet(AccessType.Peek))
          return UserRecordPermission.AllowPeekAll;
        return UserRecordPermission.AllowNone;
      }
      var mask = EntityMemberMask.Create(entity, properties);
      return new UserRecordPermission(accessType, mask); 
    }

  }//class



}
