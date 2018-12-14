﻿using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Model;

namespace Vita.Entities.Model.Construction {

  public enum AttributeApplyOrder {
    // System 
    System = 0, // Framework applies PK explicitly

    Early = 30,
    Default = 40,
    Late = 50,
  }

  /// <summary>The base class for entity model attributes. Implements IEntityModelAttributeHandler interface, 
  /// so all standard attributes are self-handling.</summary>
  public abstract class EntityModelAttributeBase : Attribute {
    protected internal EntityInfo HostEntity; // only for attrs on entity
    protected internal EntityMemberInfo HostMember; //only for attrs on members
    internal bool Validated;


    public virtual AttributeApplyOrder ApplyOrder => AttributeApplyOrder.Default;
    public virtual void Validate(IActivationLog log) { }
    public virtual void ApplyOnEntity(EntityModelBuilder builder) {  }
    public virtual void ApplyOnMember(EntityModelBuilder builder) {  }

    // Used in error messages
    public string GetHostRef() {
      var name = HostEntity.EntityType.Name;
      if(HostMember != null)
        name += "." + HostMember.MemberName;
      return name; 
    }
  }


}