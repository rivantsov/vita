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

    public void Set(EntityMemberInfo member, bool value = true) {
      Bits.Set(member.ValueIndex, value);
    }

    public bool IsSet(EntityMemberInfo member) {
      return Bits.Get(member.ValueIndex);
    }

    public string AsHexString() {
      return Bits.ToHex(); 
    }
  }
}
