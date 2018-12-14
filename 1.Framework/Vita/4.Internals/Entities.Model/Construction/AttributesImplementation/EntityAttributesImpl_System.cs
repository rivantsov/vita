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

    public override void Validate(IActivationLog log) { 
      base.Validate(log);
      if(HostMember.Kind != EntityMemberKind.EntityRef) {
        log.Error("{0} may be used only on properties that are references to other entities. Property: {1}",
          this.GetType().Name, this.GetHostRef());
      }
    }
  }//class

  public partial class OneToManyAttribute  {

    public override void Validate(IActivationLog log) {
      base.Validate(log);
      if(HostMember.Kind != EntityMemberKind.EntityList)
        log.Error("Property {0}: OneToMany attribute can be used only on entity list properties.", GetHostRef());
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
    public override void Validate(IActivationLog log) {
      base.Validate(log);
      if(this.HostMember == null && string.IsNullOrWhiteSpace(this.MemberNames)) {
        log.Error("Entity {0}: Index/key attribute ({1}) on entity may not have empty member list.", GetHostRef(), this.GetAttributeName());
      }
    }

    public override void ApplyOnEntity(EntityModelBuilder builder) {
      CreateKey(builder.Log);
    }
    public override void ApplyOnMember(EntityModelBuilder builder) {
      CreateKey(builder.Log);
    }

    public virtual void CreateKey(IActivationLog log) {
      if(this.Key != null) //protect against multiple processing
        return;
      // we initially assign temp names
      this.Key = new EntityKeyInfo(HostEntity, KeyType, HostMember, this.Alias);
      // add members
      if(HostMember != null) {
        Key.KeyMembers.Add(new EntityKeyMemberInfo(HostMember, false));
      } else {
        // it will add errors if it fails
        HostEntity.TryParseKeySpec(this.MemberNames, log, out Key.KeyMembers, ordered: true);
      }
      // construct name
      Key.ExplicitDbKeyName = this.DbKeyName; 
      // PK
      if(KeyType.IsSet(KeyType.PrimaryKey)) {
        if(HostEntity.PrimaryKey == null) {
          HostEntity.PrimaryKey = Key;
          Key.KeyMembers.Each(km => km.Member.Flags |= EntityMemberFlags.PrimaryKey);
        } else
          log.Error("Entity {0} has more than one Primary Key specified.", GetHostRef());
      }

    }
  }

  public partial class PrimaryKeyAttribute {

    public override void Validate(IActivationLog log) {
      base.Validate(log);
      KeyType = KeyType.SetFlag(KeyType.Clustered, Clustered);
    }
    // No Apply method overrides, base methods do the job
  } //class


  public partial class IndexAttribute {

    public override void Validate(IActivationLog log) {
      base.Validate(log);
      KeyType = KeyType.SetFlag(KeyType.Clustered, this.Clustered).SetFlag(KeyType.Unique, this.Unique);
      if(HostMember != null && string.IsNullOrEmpty(this.MemberNames))
        this.MemberNames = HostMember.MemberName;
    }

    public override void CreateKey(IActivationLog log) {
      if(this.Key == null) { //protect against multiple processing
        base.CreateKey(log);
        ProcessIndexIncludesAndFilters(log);
      }
    }

    private void ProcessIndexIncludesAndFilters(IActivationLog log) {
      if (!string.IsNullOrWhiteSpace(this.Filter))
        Key.IndexFilter = EntityModelBuilder.ParseFilter(this.Filter, this.HostEntity, log);
      // Check include fields
      if(!string.IsNullOrWhiteSpace(this.IncludeMembers))
        if(EntityModelBuilderHelper.TryParseKeySpec(HostEntity, this.IncludeMembers, log, 
            out List<EntityKeyMemberInfo> keyMembers, ordered: false)) {
          Key.IncludeMembers.AddRange(keyMembers.Select(km => km.Member));
      }
    }
    #endregion 

  }//class



}//ns
