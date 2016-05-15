using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization.Runtime {
  public class UserEntityTypePermission : UserRecordPermission {
    public bool HasFilter;
    public static readonly UserEntityTypePermission Empty = new UserEntityTypePermission();

    public UserEntityTypePermission() { }
    public UserEntityTypePermission(AccessType accessTypes, EntityMemberMask memberMask = null) 
      : base(accessTypes, memberMask) { }

  }
}
