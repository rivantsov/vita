using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization.Runtime {

  public class OperationPermission {

    public enum PermissionStatus {
      Denied,
      AllowMasked,
      AllowAll,
    }

    public AccessType Operation; //Peek, ReadStrict or UpdateStrict
    public EntityMemberMask Mask;
    public PermissionStatus Status; 

    private const AccessType AllowedOperations = AccessType.Peek | AccessType.ReadStrict | AccessType.UpdateStrict;

    public OperationPermission(AccessType operation, bool allow, EntityMemberMask mask = null) {
      Util.Check((operation | AllowedOperations) == AllowedOperations, 
        "Invalid operation value - only Peek, ReadStrict and UpdateStrict allowed.");
      Operation = AllowedOperations & operation;
      Mask = mask;
      if(!allow)
        Status = PermissionStatus.Denied;
      else if(Mask == null)
        Status = PermissionStatus.AllowAll;
      else
        Status = PermissionStatus.AllowMasked; 
    }

    public override string ToString() {
      return Status.ToString(); 
    }

    public bool Allowed() {
      return Status != PermissionStatus.Denied; 
    }
    public bool Allowed(EntityMemberInfo member) {
      //TODO: put it into Member Masks
      if(member.Flags.IsSet(EntityMemberFlags.IsSystem))
        return true; 
      switch(Status) {
        case PermissionStatus.Denied: return false;
        case PermissionStatus.AllowMasked: return Mask.IsSet(member);
        case PermissionStatus.AllowAll: return true; 
      }
      return true; 
    }

    internal void Merge(OperationPermission other) {
      Util.Check(this.Operation == other.Operation, "Operation must match for merge.");
      switch(Status) {
        case PermissionStatus.AllowAll: return; 
        case PermissionStatus.Denied: 
          this.Status = other.Status;
          this.Mask = other.Mask;
          return; 
        case PermissionStatus.AllowMasked: 
          switch(other.Status) {
            case PermissionStatus.Denied: return; 
            case PermissionStatus.AllowAll: this.Status = PermissionStatus.AllowAll; return;
            case PermissionStatus.AllowMasked: this.Mask.Or(other.Mask); return; 
          }
          break; 
      }
    }//method
    
  }//class
}
