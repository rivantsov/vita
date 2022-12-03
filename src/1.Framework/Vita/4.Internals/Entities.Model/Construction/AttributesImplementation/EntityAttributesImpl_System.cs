using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Model.Construction;

namespace Vita.Entities {

  #region EntityRef, OneToMany, ManyToMany 
  // EntityREf, OneToMany, ManyToMany are attributes with empty Apply methods - the EntityModelBuilder uses them as info containers and applies the logic explicitly
  public partial class EntityRefAttribute : EntityModelAttributeBase {

    public override void Validate(ILog log) { 
      base.Validate(log);
      if(HostMember.Kind != EntityMemberKind.EntityRef) {
        log.LogError(
          $"{this.GetType().Name} may be used only on properties that are references to other entities. Property: {HostRef}");
      }
    }
  }//class

  public partial class OneToManyAttribute  {

    public override void Validate(ILog log) {
      base.Validate(log);
      if(HostMember.Kind != EntityMemberKind.EntityList)
        log.LogError($"Property {HostRef}: OneToMany attribute can be used only on entity list properties.");
    }
  }// class

  public partial class ManyToManyAttribute { }

  #endregion

  #region KeyAttribute, PrimaryKeyAttribute, IndexAttribute

  public abstract partial class KeyAttribute : EntityModelAttributeBase {
    public KeyType KeyType;
    public string MemberNames;
    public string DbKeyName;
    public string Alias; //friendly alias; used in UniqueIndexViolationException
    internal EntityKeyInfo Key;

    public override AttributeApplyOrder ApplyOrder => AttributeApplyOrder.System;

    // You cannot use it directly
    internal protected KeyAttribute(KeyType keyType) {
      KeyType = keyType;
    }
    public override void Validate(ILog log) {
      base.Validate(log);
      if(this.HostMember == null && string.IsNullOrWhiteSpace(this.MemberNames)) {
        log.LogError($"{HostRef}: Index/key attribute ({this.GetType()}) on entity may not have empty member list.");
      }
      if (this.HostMember != null && !string.IsNullOrWhiteSpace(this.MemberNames)) {
        log.LogError($"{HostRef}: Index/key attribute ({this.GetType()}) on member should not have explicit member list.");
      }
    }

    public override void ApplyOnEntity(EntityModelBuilder builder) {
      CreateKey(builder.Log);
    }
    public override void ApplyOnMember(EntityModelBuilder builder) {
      CreateKey(builder.Log);
    }

    public virtual void CreateKey(ILog log) {
      this.Key = new EntityKeyInfo(HostEntity, KeyType, HostMember, this);
      // PK
      if(KeyType.IsSet(KeyType.PrimaryKey)) {
        if(HostEntity.PrimaryKey == null) {
          HostEntity.PrimaryKey = Key;
        } else
          log.LogError($"Entity {HostEntity.Name} has more than one Primary Key specified.");
      }
    } // CreateKey

  }

  public partial class PrimaryKeyAttribute {

    public override void Validate(ILog log) {
      base.Validate(log);
      KeyType = KeyType.SetFlag(KeyType.Clustered, Clustered);
    }
    // No Apply method overrides, base methods do the job
  } //class


  public partial class IndexAttribute {

    public override void Validate(ILog log) {
      base.Validate(log);
      KeyType = KeyType.SetFlag(KeyType.Clustered, this.Clustered).SetFlag(KeyType.Unique, this.Unique);
      if(HostMember != null && string.IsNullOrEmpty(this.MemberNames))
        this.MemberNames = HostMember.MemberName;
    }

    public override void CreateKey(ILog log) {
      base.CreateKey(log);
      Key.FilterSpec = this.Filter;
      Key.IncludeMembersSpec = this.IncludeMembers;
    }

   #endregion 

  }//class
}//ns
