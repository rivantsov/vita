using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization {

  /// <summary>An abstract permission class. May be used as a base class for defining custom permission classes implementing custom authorization logic. </summary>
  public abstract class Permission : HashedObject {
    public string Name; 

    public Permission(string name) {
      Name = name; 
    }

    public override string ToString() {
      return Name;
    }
  }

}
