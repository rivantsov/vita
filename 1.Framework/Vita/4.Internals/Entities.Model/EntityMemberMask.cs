using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using Vita.Entities.Utilities;

namespace Vita.Entities.Model {
  public class EntityMemberMask {
    public BitMask Bits;

    public EntityMemberMask(int length, bool setAll = false) {
      Bits = new BitMask(length, setAll);
    }

    public bool IsSet(EntityMemberInfo member) {
      return Bits.Get(member.Index);
    }

    public string AsHexString() {
      return Bits.ToHex(); 
    }
  }
}
