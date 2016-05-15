using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Authorization {
  public class ObjectAccessPermission : Permission {
    public readonly AccessType AccessType;
    public Type[] Types;
    public ObjectAccessPermission(string name, AccessType accessType, params Type[] types) : base(name) {
      AccessType = accessType;
      Types = types;
    }
  }
}
