using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Model;
using Vita.Common;

namespace Vita.Entities.Model {

  //Mask, one bit per member
  //Used for read mask and write masks in entity records - in Authorization rules
  
  public class EntityMemberMask {
    public string Key; //some name-like identifier to use in ToString() method
    public EntityInfo Entity;
    public CustomBitArray Bits; 

    public EntityMemberMask(string key, EntityInfo entity) {
      Key = key;
      Entity = entity;
      Bits = new CustomBitArray(Entity.Members.Count);
    }
    public EntityMemberMask(EntityMemberMask clone) {
      Key = clone.Key;
      Entity = clone.Entity;
      Bits = new CustomBitArray(clone.Bits);
    }

    public bool IsSet(EntityMemberInfo member) {
      return Bits[member.ValueIndex];
    }

    public void Set(EntityMemberInfo member, bool value = true) {
      Bits[member.ValueIndex] = value;
    }

    public bool AllZero() {
      return Bits.AllZero();
    }
    public bool AllSet() {
      return Bits.AllSet();
    }

    public EntityMemberMask Clone() {
      return new EntityMemberMask(this);
    }

    public EntityMemberMask Or(EntityMemberMask other) {
      Key = null; 
      Bits.Or(other.Bits);
      return this; 
    }

    public EntityMemberMask And(EntityMemberMask other) {
      Key = null;
      Bits.And(other.Bits);
      return this;
    }

    public EntityMemberMask Not() {
      Key = null;
      Bits.Not();
      return this;
    }

    public override string ToString() {
      if (!string.IsNullOrWhiteSpace(Key)) 
        return Key;
      var memberNames = new StringList();
      foreach (var member in Entity.Members)
        if (Bits[member.ValueIndex])
          memberNames.Add(member.MemberName);
      return Entity.Name + ":" + string.Join(",", memberNames); 
    }

    public static EntityMemberMask Create(EntityInfo entity, string propertiesOrGroups) {
      var invalidNames = new StringList();
      var mask = new EntityMemberMask(propertiesOrGroups, entity);
      var props = propertiesOrGroups.SplitNames(',', ';');
      foreach (var name in props) {
        //first try member
        if (string.IsNullOrWhiteSpace(name))
          continue;
        var grp = entity.GetPropertyGroup(name);
        if (grp != null) {
          foreach (var m in grp.Members)
            mask.Set(m);
          continue;
        }
        var member = entity.GetMember(name);
        if (member != null) {
          mask.Set(member);
          continue;
        }
        //name is invalid
        invalidNames.Add(name);
      }
      if (invalidNames.Count > 0)
        Util.Throw("Properties/subgroups [{0}] not found in entity {1}.", string.Join(",", invalidNames), entity.EntityType);
      return mask;
    }
  
  }//class
}
