using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Vita.Common;

using Vita.Entities.Model;
using Vita.Entities.Model.Construction;

namespace Vita.Entities {
  // Special framework attributes, known to core framework and applied using specific internal logic

  public abstract class SpecialModelAttribute : EntityModelAttributeBase {
    public SpecialModelAttribute() {
      base.ApplyOrder = AttributeApplyOrder.System;
    }
  }

  public partial class EntityAttribute : SpecialModelAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      if (!string.IsNullOrWhiteSpace(this.Name))
        entity.Name = this.Name;
      entity.TableName = this.TableName;
    }
  } //class

  public partial class ForEntityAttribute : SpecialModelAttribute { }

  public partial class ColumnAttribute : SpecialModelAttribute {

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      if (!string.IsNullOrWhiteSpace(this.ColumnName)) member.ColumnName = this.ColumnName;
      if (!string.IsNullOrWhiteSpace(this.Default)) member.ColumnDefault = this.Default;
      member.Scale = this.Scale;
      if (this.Precision > 0) member.Precision = this.Precision;
      if (this.Size != 0)
        member.Size = this.Size;
      member.ExplicitDbType = this._dbType;
      member.ExplicitDbTypeSpec = this.DbTypeSpec;
    }
  }// class

  public partial class NoColumnAttribute : SpecialModelAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.Kind = MemberKind.Transient;
      member.GetValueRef = MemberValueGettersSetters.GetTransientValue;
      member.SetValueRef = MemberValueGettersSetters.SetTransientValue;
    }
  }// class

  public partial class SizeAttribute : SpecialModelAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      // Check size code and lookup in tables
      if (!string.IsNullOrEmpty(this.SizeCode)) {
        var sizeTable = context.Model.App.SizeTable;
        //If there is size code, look it up in SizeTable; first check module-specific value, then global value for the code
        int size;
        var fullCode = Sizes.GetFullSizeCode(member.Entity.EntityType.Namespace, this.SizeCode); 
        if (sizeTable.TryGetValue(fullCode, out size)) { //check full code with module's namespace prefix
          member.Size = size;
          return; 
        }
        if (sizeTable.TryGetValue(this.SizeCode, out size)) { //check global value, non-module-specific
          member.Size = size;
          return; 
        }
      }
      //If size is specified explicitly, use it
      if(this.Size > 0) {
        member.Size = Size;
        if((this.Options & SizeOptions.AutoTrim) != 0)
          member.Flags |= EntityMemberFlags.AutoTrim; 
        return;
      }
      //If no Size code and no value, it is an error, lookup in module settings
      context.Log.Error("Property {0}.{1}: invalid Size attribute, must specify size code or value", member.Entity.Name, member.MemberName);
    }
  }// class

  public partial class NullableAttribute {

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.Flags |= EntityMemberFlags.Nullable;
      if (member.DataType.IsValueType) {
        member.Flags |= EntityMemberFlags.ReplaceDefaultWithNull;
        member.GetValueRef = MemberValueGettersSetters.GetValueTypeReplaceNullWithDefault;
        member.SetValueRef = MemberValueGettersSetters.SetValueTypeReplaceDefaultWithNull;
      }
    }
  }// class


  public partial class PrimaryKeyAttribute : SpecialModelAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      if (string.IsNullOrWhiteSpace(this.MemberNames)) {
        context.Log.Error("Entity {0}: primary key must specify property name(s) when used on Entity interface.", entity.FullName);
        return;
      }
      if (!CreatePrimaryKey(context, entity)) 
        return;
      var error = false; 
      var pkMembers = EntityAttributeHelper.ParseMemberNames(entity, this.MemberNames, errorAction: mn => {
        context.Log.Error("Entity {0}: property {1} listed in PrimaryKey attribute not found.", entity.FullName, mn);
        error = true; 
      });
      if(error)
        return;
      foreach(var km in pkMembers) {
        km.Member.Flags |= EntityMemberFlags.PrimaryKey;
        entity.PrimaryKey.KeyMembers.Add(km); 
      }
    }

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      var entity = member.Entity;
      if (!CreatePrimaryKey(context, entity)) return;
      entity.PrimaryKey.KeyMembers.Add(new EntityKeyMemberInfo(member, false));
      member.Flags |= EntityMemberFlags.PrimaryKey;
    }

    private bool CreatePrimaryKey(AttributeContext context, EntityInfo entity) {
      if (entity.PrimaryKey != null) {
        context.Log.Error("More than one primary key specified on entity {0}", entity.FullName);
        return false; 
      }
      entity.PrimaryKey = new EntityKeyInfo("PK_" + entity.Name, KeyType.PrimaryKey, entity);
      if(IsClustered)
        entity.PrimaryKey.KeyType |= KeyType.Clustered;
      return true; 
    }
  } //class

  public partial class OneToManyAttribute : SpecialModelAttribute {

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      var listInfo = member.ChildListInfo = new ChildEntityListInfo(member);
      listInfo.RelationType = EntityRelationType.ManyToOne;
      var entType = member.Entity.EntityType;
      var targetType = member.DataType.GetGenericArguments()[0];
      listInfo.TargetEntity = context.Model.GetEntityInfo(targetType, true);
      if (!string.IsNullOrWhiteSpace(this.ThisEntityRef)) {
        var fkMember = listInfo.TargetEntity.GetMember(this.ThisEntityRef);
        if (fkMember == null) {
          context.Log.Error("EntityList member {0}.{1}: could not find property {2} in target entity. ",
            entType, member.MemberName, this.ThisEntityRef);
          return;
        }
        this.ThisEntityRef = fkMember.MemberName;
        listInfo.ParentRefMember = fkMember;
      } else
        listInfo.ParentRefMember = listInfo.TargetEntity.FindEntityRefMember(this.ThisEntityRef, entType, member, context.Log);
      //Check that reference is found
      if(listInfo.ParentRefMember == null)
        context.Log.Error("EntityList member {0}.{1}: could not find reference property in target entity. ", entType, member.MemberName);
      else
        //Set back reference to list from ref member
        listInfo.ParentRefMember.ReferenceInfo.TargetListMember = member;
      listInfo.Filter = this.Filter; 
    }
  }// class

  public partial class ManyToManyAttribute : SpecialModelAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      var linkEntityType = this.LinkEntity;
      var listInfo = member.ChildListInfo = new ChildEntityListInfo(member);
      listInfo.RelationType = EntityRelationType.ManyToMany;
      listInfo.LinkEntity = context.Model.GetEntityInfo(linkEntityType, true);
      listInfo.ParentRefMember = listInfo.LinkEntity.FindEntityRefMember(this.ThisEntityRef, member.Entity.EntityType, member, context.Log);
      if(listInfo.ParentRefMember == null) {
        context.Log.Error( "Many-to-many setup error: back reference to entity {0} not found in link entity {1}.", member.Entity.EntityType, LinkEntity);
        return; 
      }
      listInfo.ParentRefMember.ReferenceInfo.TargetListMember = member; 
      var targetEntType = member.DataType.GetGenericArguments()[0];
      listInfo.OtherEntityRefMember = listInfo.LinkEntity.FindEntityRefMember(this.OtherEntityRef, targetEntType, member, context.Log);
      if (listInfo.OtherEntityRefMember != null)
        listInfo.TargetEntity = context.Model.GetEntityInfo(listInfo.OtherEntityRefMember.DataType, true);
    }//method
  }// class

  public partial class EntityRefAttribute : SpecialModelAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      if (member.Kind != MemberKind.EntityRef) {
        context.Log.Error("EntityRef attribute may be used only on properties that are references to other entities. Property: {0}.{1}",
            member.Entity.Name, member.MemberName);
        return;
      }
      var entity = member.Entity;
      // Create reference info
      var targetType = member.DataType; //.Property.PropertyType;
      var targetEntity = context.Model.GetEntityInfo(targetType);
      Util.Check(targetEntity != null, "Target entity not found: {0}", targetType);
      //Create foreign key
      ForeignKeyName = ForeignKeyName ?? "FK_" + entity.Name + "_" + member.MemberName;
      var fk = new EntityKeyInfo(ForeignKeyName, KeyType.ForeignKey, entity, member);
      fk.KeyMembers.Add(new EntityKeyMemberInfo(member, false));
      member.ReferenceInfo = new EntityReferenceInfo(member, fk, targetEntity.PrimaryKey);
      member.ReferenceInfo.ForeignKeyColumns = this.KeyColumns;
    }
  }//class

  public partial class OldNamesAttribute : SpecialModelAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      entity.OldNames = StringHelper.SplitNames(this.OldNames);
    }

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.OldNames = StringHelper.SplitNames(this.OldNames);
    }

  }// class




}
