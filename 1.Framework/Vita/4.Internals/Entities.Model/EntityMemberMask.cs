using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace Vita.Entities.Model {
  public class EntityMemberMask {
    public BitArray Mask;

    public EntityMemberMask() {

    }

    public bool IsSet(EntityMemberInfo member) {
      return Mask.Get(member.Index);
    }
  }
}
