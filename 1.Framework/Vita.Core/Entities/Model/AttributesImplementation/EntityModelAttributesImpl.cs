using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

using Vita.Common;
using Vita.Entities.Model.Construction;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Model;
using Vita.Entities.Authorization;
using Vita.Entities.Authorization.Runtime;

namespace Vita.Entities {

  // Non-special model attributes contain their own code and system does not know any specifics about them. 
  // System calls their Apply methods, and attributes do all needed stuff

  // Model attributes are self-handlers
  public abstract class EntityModelAttributeBase : Attribute, IAttributeHandler {
    public virtual AttributeApplyOrder ApplyOrder { get; protected set; }
    public virtual void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) { }
    public virtual void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) { }
    public virtual CustomAttributeBuilder Clone(Attribute attribute) {
      return null;
    }
    public EntityModelAttributeBase() {
      ApplyOrder = AttributeApplyOrder.Default;
    }
    protected string GetAttributeName() {
      const string suffix = "Attribute";
      var name = this.GetType().Name;
      if(name.EndsWith(suffix))
        name = name.Substring(0, name.Length - suffix.Length);
      return name;

    }
  }

  public partial class PersistOrderInAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      if (member.Kind != MemberKind.EntityList) {
        context.Log.Error("PersistOrderIn attribute may be specified only on list members. Entity {0}.", member.Entity.Name);
        return; 
      }
      EntityInfo orderedEntity = null; 
      //determine the entity that is ordered
      switch (member.ChildListInfo.RelationType) {
        case EntityRelationType.ManyToOne:   orderedEntity = member.ChildListInfo.TargetEntity; break;
        case EntityRelationType.ManyToMany: orderedEntity = member.ChildListInfo.LinkEntity; break; 
      }
      //check that there is a member 
      var orderMember = orderedEntity.GetMember(this.Property);
      if (orderMember == null) {
        context.Log.Error("Property '{0}' referenced in PersistOrderIn attribute on entity {1} not found in entity {2}.",
          this.Property, member.Entity.Name, orderedEntity.Name);
        return; 
      }
      //current limitation - index property must be int32 only
      if (orderMember.DataType != typeof(Int32)) {
        context.Log.Error("Invalid data type ({0}) for property '{1}' referenced in PersistOrderIn attribute on entity {2}: must be Int32.",
          orderMember.DataType.Name, this.Property, member.Entity.Name);
        return;
      }
      // Validation passed, assign order member
      member.ChildListInfo.PersistentOrderMember = orderMember;
      // Make list order to be by orderMember
      var fromKey = member.ChildListInfo.ParentRefMember.ReferenceInfo.FromKey;
      fromKey.OrderByForSelect = new[] { new EntityKeyMemberInfo(orderMember, desc: false) };
    }
  }

  public partial class OrderByAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      if (entity.DefaultOrderBy != null)
        context.Log.Error("More than one OrderBy attribute in entity {0}.", entity.Name);
      ConstructOrderBy(context, entity);
    }

    //This is a special case - OrderBy attribute specifies the order of entities in list property.
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      var entity = member.Entity; 
      if (member.Kind != MemberKind.EntityList) {
        context.Log.Error("OrderBy attribute may be used only on entities or list properties. Entity: {0}, property: {1}", 
             entity.Name, member.MemberName);
        return; 
      }
      var fromKey = member.ChildListInfo.ParentRefMember.ReferenceInfo.FromKey;
      fromKey.OrderByForSelect  = EntityAttributeHelper.ParseMemberNames(member.ChildListInfo.TargetEntity, this.OrderByList, ordered: true, 
          errorAction: spec => {
            context.Log.Error("Invalid member/spec: {0} in {1} attribute, property {2}.{3}", spec, this.GetAttributeName(), entity.Name, member.MemberName);
          });
    }

    private void ConstructOrderBy(AttributeContext context, EntityInfo entity) {
      bool error = false; 
      entity.DefaultOrderBy = EntityAttributeHelper.ParseMemberNames(entity, OrderByList, ordered: true,
           errorAction: spec => {
               context.Log.Error("Invalid member/spec: {0} in {1} attribute on entity {2}", spec, this.GetAttributeName(), entity.Name);
               error = true; 
      });
      if (error) return; 
      //Check that they are real cols
      foreach(var ordM in entity.DefaultOrderBy) {
        if (ordM.Member.Kind != MemberKind.Column) 
          context.Log.Error("Invalid property {0} in OrderBy attribute in entity {1} - must be a plain property.", 
            ordM.Member.MemberName, entity.Name);
      }
    }

  }// class

  public partial class PropertyGroupAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      if (string.IsNullOrWhiteSpace(this.GroupName)) {
        context.Log.Error("Group name may not be empty. Entity: {0}.", entity.Name);
        return; 
      }
      var names =  StringHelper.SplitNames(this.MemberNames);
      foreach (var name in names) {
        var member = entity.GetMember(name);
        if (member == null) {
          context.Log.Error("PropertyGroup '{0}', entity {1}: member {2} not found.", this.GroupName, entity.Name, name);
          continue; 
        }
        var grp = entity.GetPropertyGroup(this.GroupName, create: true);
        if (!grp.Members.Contains(member))
          grp.Members.Add(member); 
      }//foreach
    }
  } //class

  public partial class GroupsAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      if (string.IsNullOrWhiteSpace(this.GroupNames))
        return; 
      var names = StringHelper.SplitNames(this.GroupNames);
      foreach (var name in names) {
        if (string.IsNullOrWhiteSpace(name))
          continue; 
        var grp = member.Entity.GetPropertyGroup(name, create: true);
        if (!grp.Members.Contains(member))
          grp.Members.Add(member);
      }//foreach
    }
  } //class

  public partial class NoUpdateAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.Flags |= EntityMemberFlags.NoDbUpdate;
    }
  }// class

  public partial class NoInsertUpdateAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.Flags |= EntityMemberFlags.NoDbInsert | EntityMemberFlags.NoDbUpdate;
    }
  }// class

  public partial class DateOnlyAttribute {
    private Action<EntityRecord, EntityMemberInfo, object> _defaultSetter;

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      var dataType = member.DataType;
      if (dataType.IsGenericType)
        dataType = dataType.GetGenericArguments()[0];
      bool isDateTime = dataType == typeof(DateTime) || dataType == typeof(DateTimeOffset);
      if (!isDateTime) {
        context.Log.Error("Property {0}.{1}: DateOnly attribute may be specified only on DataTime or DateTimeOffset properties. ",  
          member.Entity.Name, member.MemberName);
        return; 
      }
      //Inject interceptor
      _defaultSetter = member.SetValueRef;
      if (dataType == typeof(DateTime))
        member.SetValueRef = this.SetValueDateTime;
      else
        member.SetValueRef = this.SetValueDateTimeOffset;
    }

    //Interceptors for SetValue
    void SetValueDateTime(EntityRecord record, EntityMemberInfo member, object value) {
      if (value != null && value != DBNull.Value) {
        if (value.GetType() == typeof(DateTime?))
          value = (DateTime?)((DateTime?)value).Value.Date;
        else
          value = ((DateTime)value).Date;
      }// if value != null
      _defaultSetter(record, member, value);
    }

    void SetValueDateTimeOffset(EntityRecord record, EntityMemberInfo member, object value) {
      if (value != null && value != DBNull.Value) {
        if (value.GetType() == typeof(DateTimeOffset?)) {
          var date = ((DateTimeOffset?)value).Value.Date;
          value = (DateTimeOffset?) (new DateTimeOffset(date));
        }
        else
          value = ((DateTimeOffset)value).Date;
      }// if value != null
      _defaultSetter(record, member, value);
    }
  }// class

  public partial class UtcAttribute {
    Func<EntityRecord, EntityMemberInfo, object> _defaultGetter;
    Action<EntityRecord, EntityMemberInfo, object> _defaultSetter; 

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      var dataType = member.DataType;
      member.Flags |= EntityMemberFlags.Utc;
      if (member.DataType != typeof(DateTime) && member.DataType != typeof(DateTime?)) {
        context.Log.Error("Utc attribute may be specified only on DataTime properties. Entity.Member: {0}.{1}", member.Entity.Name, member.MemberName);
        return;
      }
      //Inject interceptor
      _defaultGetter = member.GetValueRef;
      _defaultSetter = member.SetValueRef;
      member.GetValueRef = this.GetValueInterceptor;
      member.SetValueRef = this.SetValueInterceptor;
    }
    //Interceptor for SetValue
    void SetValueInterceptor(EntityRecord record, EntityMemberInfo member, object value) {
      var utcValue = ToUtc(value); 
      _defaultSetter(record, member, utcValue);
    }

    object GetValueInterceptor(EntityRecord record, EntityMemberInfo member) {
      var value = _defaultGetter(record, member); 
      var utcValue = ToUtc(value);
      return utcValue; 
    }

    private object ToUtc(object value) {
      if (value == null || value == DBNull.Value)
        return value; 
      DateTime dtValue;
      if (value.GetType() == typeof(DateTime?))
        dtValue = ((DateTime?)value).Value;
      else
        dtValue = (DateTime)value;
      // If value is coming from database or from serialization, its Kind shows Unspecified - set it to UTC explicitly; as we know that it is in fact UTC
      //if (record.Status == EntityStatus.Loading && dtValue.Kind == DateTimeKind.Unspecified)
      switch (dtValue.Kind) {
        case DateTimeKind.Utc: return dtValue; 
        case DateTimeKind.Local: return dtValue.ToUniversalTime();
        case DateTimeKind.Unspecified: return new DateTime(dtValue.Ticks, DateTimeKind.Utc); //assume it is already UTC value, but need to recreate with proper kind
        default: return dtValue; // just to supress compiler error
      }
    } 
  }// class


  public partial class UnlimitedAttribute {
    public UnlimitedAttribute() : base() {  }
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.Flags |= EntityMemberFlags.UnlimitedSize;
      member.Size = -1;
    }

  }// class

  public partial class CurrencyAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.ExplicitDbType = DbType.Currency;
    }
  }

  public partial class ComputedAttribute {
    private MethodInfo _method;

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      if (member.ClrMemberInfo.HasSetter()) {
        context.Log.Error("Computed property {0}.{1} may not have a setter, it is readonly.", member.Entity.EntityType.Name, member.MemberName);
        return; 
      }
      if (!this.Persist)
        member.Kind = MemberKind.Transient;
      member.Flags |= EntityMemberFlags.Computed;
      _method = this.MethodClass.GetMethod(this.MethodName);
      if (_method == null) {
        context.Log.Error("Method {0} for computed column {1} not found in type {2}",
          this.MethodName, member.MemberName, this.MethodClass);
        return; 
      }
      member.GetValueRef = GetComputedValue;
      member.SetValueRef = MemberValueGettersSetters.DummySetValue;
    }

    public object GetComputedValue(EntityRecord record, EntityMemberInfo member) {
      if (_method == null) return null;
      var value = _method.Invoke(null, new object[] { record.EntityInstance });
      if (Persist)
        record.SetValueDirect(member, value); 
      return value; 
    }

  }// class

  public partial class DependsOnAttribute {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      var entity = member.Entity;
      var namesArr = StringHelper.SplitNames(this.MemberNames);
      foreach (var name in namesArr) {
        var targetMember = entity.GetMember(name);
        if (targetMember == null) {
          context.Log.Error("Member {0} referenced in DependsOn attribute on member {1}.{2} not found.", name, entity.Name, member.MemberName);
          continue; 
        }
        //add this member to DependentMembers array of targetMember
        if (targetMember.DependentMembers == null)
          targetMember.DependentMembers = new EntityMemberInfo[] { member };
        else {
          var mList = targetMember.DependentMembers.ToList(); 
          mList.Add(member);
          targetMember.DependentMembers = mList.ToArray(); 
        }
      }//foreach name
    } // method
  }//class

  public partial class ValidateAttribute {
    MethodInfo _method;

    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      _method = MethodClass.GetMethod(MethodName);
      if (_method == null)
        context.Log.Error("Method {0} specified as Validation method for entity {1} not found in type {2}",
            MethodName, entity.EntityType, MethodClass);
      entity.Events.ValidatingChanges += Events_Validating;
    }

    void Events_Validating(EntityRecord record, EventArgs args) {
      _method.Invoke(null, new object[] {record.EntityInstance });
    }
  }// class

  public partial class PagedAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      entity.PagingMode = PagingMode.DataStore;
    }
  }// class

  public partial class AutoAttribute : EntityModelAttributeBase {
    EntityMemberInfo _member;
    SequenceDefinition _sequence; 

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      _member = member;
      var entity = member.Entity;
      member.Flags |= EntityMemberFlags.AutoValue;
      member.AutoValueType = this.Type;
      switch (this.Type) {
        case AutoType.Identity:
          entity.Flags |= EntityFlags.HasIdentity;
          member.Flags |= EntityMemberFlags.Identity | EntityMemberFlags.NoDbInsert | EntityMemberFlags.NoDbUpdate;
          //Usually identity is int (or long). But there are some wierd real-life databases with Numeric (Decimal) identity columns
          // apparently MS SQL allows this
          var intOrDec = member.DataType.IsInt() || member.DataType == typeof(Decimal);
          if (!intOrDec) {
            context.Log.Error("Entity member {0}.{1}, type {2}: Identity attribute may be set only on member of integer or decimal types. ",
              _member.Entity, _member.MemberName, this.Type);
            return; 
          }
          entity.Events.New += EntityEvent_NewEntityHandleIdentity;
          entity.SaveEvents.SubmittedChanges += EntityEvent_IdentityEntitySubmitted;
          break;

        case AutoType.Sequence:
          if (!member.DataType.IsInt()) {
            context.Log.Error("Entity member {0}.{1}, type {2}: Sequence attribute may be set only on member of integer types. ",
              _member.Entity, _member.MemberName, this.Type);
            return;
          }
          if (string.IsNullOrWhiteSpace(SequenceName)) {
            context.Log.Error("Entity member {0}.{1}: Sequence name must be specified.", _member.Entity, _member.MemberName);
            return;
          }
          _sequence = context.Model.FindSequence(SequenceName, entity.Module);
          if (_sequence == null) {
            context.Log.Error("Entity member {0}.{1}: Sequence {0} not defined.", _member.Entity, _member.MemberName, this.SequenceName);
            return; 
          }
          if (_sequence.DataType != member.DataType) {
            context.Log.Error("Entity member {0}.{1}: data type {2} does not match sequence '{3}' data type {4}.", _member.Entity, _member.MemberName, _member.DataType, this.SequenceName, _sequence.DataType);
            return; 
          }
          entity.Events.New += EntityEvent_NewEntityHandleSequence;
          break;

        case AutoType.NewGuid:
          if (!CheckDataType(context, typeof(Guid)))
            return; 
          entity.Events.New += EntityEvent_HandleNewGuid;
          break;

        case AutoType.CreatedOn:
        case AutoType.UpdatedOn:
          if (!CheckDataType(context, typeof(DateTime), typeof(DateTimeOffset)))
            return; 
          if (this.Type == AutoType.CreatedOn)
            member.Flags |= EntityMemberFlags.NoDbUpdate;
          if (member.DataType == typeof(DateTime) || member.DataType == typeof(DateTime?))
            entity.SaveEvents.SavingChanges += EntityEvent_HandleCreatedUpdatedOnDateTime;
          else
            entity.SaveEvents.SavingChanges += EntityEvent_HandleCreatedUpdatedOnDateTimeOffset;
          break;

        case AutoType.CreatedBy:
          if (!CheckDataType(context, typeof(string)))
            return;
          entity.Events.New += EntityEvent_HandleUpdatedCreatedBy;
          member.Flags |= EntityMemberFlags.NoDbUpdate;
          break; 

        case AutoType.UpdatedBy:
          if (!CheckDataType(context, typeof(string)))
            return;
          entity.Events.New += EntityEvent_HandleUpdatedCreatedBy;
          entity.Events.Modified += EntityEvent_HandleUpdatedCreatedBy;
          break;
 
        case AutoType.CreatedById:
          entity.Events.New += EntityEvent_HandleUpdatedCreatedById;
          member.Flags |= EntityMemberFlags.NoDbUpdate;
          break;
 
        case AutoType.UpdatedById:
          entity.Events.New += EntityEvent_HandleUpdatedCreatedById;
          entity.Events.Modified += EntityEvent_HandleUpdatedCreatedById;
          break; 

        case AutoType.RowVersion:
          member.Flags |= EntityMemberFlags.RowVersion | EntityMemberFlags.NoDbInsert | EntityMemberFlags.NoDbUpdate;
          member.Entity.Flags |= EntityFlags.HasRowVersion;
          member.ExplicitDbTypeSpec = "timestamp";
          break; 
      }//swith AutoValueType
    }

    private bool CheckDataType(AttributeContext context, params Type[] types) {
      var dt = _member.DataType;
      if (dt.IsGenericType)
        dt = dt.GetGenericArguments()[0]; // check for DateTime?
      if (types.Contains(dt)) 
        return true;
      context.Log.Error("Entity member {0}.{1}: Auto({2})  attribute may be set only on member of type(s): {3}. ", 
        _member.Entity, _member.MemberName, this.Type, string.Join(", ", types.Select(t=>t.Name)));
      return false; 
    }

    /* We initialize identity fields with temp negative values; when record is saved, they will be replaced with generated identity
     * The reason for temp negative values is to make entity comparison work properly for unsaved entities
     */ 
    private long _identityCount; //used for temp values in identity attributes
    void EntityEvent_NewEntityHandleIdentity(EntityRecord record, EventArgs args) {
      if (record.SuppressAutoValues) return;
      var newIdValue = System.Threading.Interlocked.Decrement(ref _identityCount);
      var initValue = Convert.ChangeType(newIdValue, _member.DataType);
      record.SetValueDirect(_member, initValue); //0, Int32 or Int64
    }

    void EntityEvent_NewEntityHandleSequence(EntityRecord record, EventArgs args) { 
      if (record.SuppressAutoValues) return;
      object value; 
      if (_sequence.DataType == typeof(int))
        value = record.Session.GetSequenceNextValue<int>(_sequence);
      else 
        value = record.Session.GetSequenceNextValue<long>(_sequence);
      record.SetValueDirect(_member, value);
    }

    void EntityEvent_IdentityEntitySubmitted(EntityRecord record, EventArgs args) {
      if (record.Status != EntityStatus.New) return;
      var recs = record.Session.RecordsChanged;
      foreach (var refMember in record.EntityInfo.IncomingReferences) {
        var childRecs = recs.Where(r => r.EntityInfo == refMember.Entity);
        foreach (var childRec in childRecs) {
          var refBackValue = childRec.ValuesTransient[refMember.ValueIndex];
          if (refBackValue == record)
            refMember.SetValueRef(childRec, refMember, record.EntityInstance); //this will copy foreign key
        }//foreach childRec
      }
    }//method


    void EntityEvent_HandleNewGuid(EntityRecord record, EventArgs args) {
      if (record.SuppressAutoValues) return;
      var newGuid = Guid.NewGuid();
      record.SetValueDirect(_member, newGuid);
    }

    void EntityEvent_HandleUpdatedCreatedBy(EntityRecord record, EventArgs args) {
      if (record.SuppressAutoValues) return;
      var userName = record.Session.Context.User.UserName;
      record.SetValueDirect(_member, userName);
    }

    void EntityEvent_HandleUpdatedCreatedById(EntityRecord record, EventArgs args) {
      if (record.SuppressAutoValues) return;
      // Application might be using either Guids for UserIDs or Ints (int32 or Int64);
      // take this into account
      if(_member.DataType == typeof(Guid)) {
        var userId = record.Session.Context.User.UserId;
        record.SetValueDirect(_member, userId);
      } else {
        //UserID is Int (identity); use AltUserId
        var altUserId = record.Session.Context.User.AltUserId;
        object userId = _member.DataType == typeof(Int64) ? altUserId : Convert.ChangeType(altUserId, _member.DataType);
        record.SetValueDirect(_member, userId);

      }
    }

    void EntityEvent_HandleCreatedUpdatedOnDateTime(EntityRecord record, EventArgs args) {
      if (record.SuppressAutoValues) return;
      //Do CreatedOn only if it is new record
      if (this.Type == AutoType.CreatedOn && record.Status != EntityStatus.New) return;
      var dateTime = record.Session.TransactionDateTime;
      if (_member.Flags.IsSet(EntityMemberFlags.Utc))
        dateTime = dateTime.ToUniversalTime();
      record.SetValueDirect(_member, dateTime); 
    }

    void EntityEvent_HandleCreatedUpdatedOnDateTimeOffset(EntityRecord record, EventArgs args) {
      if (record.SuppressAutoValues) return;
      //Do CreatedOn only if it is new record
      if (this.Type == AutoType.CreatedOn && record.Status != EntityStatus.New) return;
      var offset = new DateTimeOffset(record.Session.TransactionDateTime);
      record.SetValueDirect(_member, offset);
    }

  }// class

  public partial class PropagageUpdatedOnAttribute : EntityModelAttributeBase {
    EntityMemberInfo _member;
    Action<EntityRecord, EntityMemberInfo, object> _defaultValueSetter;

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      base.Apply(context, attribute, member);
      if(member.Kind != MemberKind.EntityRef) {
        context.Log.Error(
          "PropagateUpdatedOn attribute may be used only on properties that are references to other entities. Property: {0}.{1}",
            member.Entity.Name, member.MemberName);
        return;
      }
      _member = member;
      //Set interceptors
      member.Entity.Events.Modified += Events_ModifyingDeleting;
      member.Entity.Events.Deleting += Events_ModifyingDeleting; 
      _defaultValueSetter = member.SetValueRef;
      member.SetValueRef = SetMemberValue; 
    }

    private void SetMemberValue(EntityRecord record, EntityMemberInfo member, object value) {
      _defaultValueSetter(record, member, value);
      MarkTargetAsModified(record); 
    }
    private void Events_ModifyingDeleting(EntityRecord record, EventArgs args) {
      MarkTargetAsModified(record); 
    }

    private void MarkTargetAsModified(EntityRecord record) {
      var target = record.GetValue(_member);
      if(target == null)
        return; 
      var targetRec =  (target as EntityBase).Record;
      if (targetRec.Status == EntityStatus.Loaded) 
        targetRec.Status = EntityStatus.Modified;
    }
  }


  public partial class HashForAttribute : EntityModelAttributeBase {
    EntityMemberInfo _member;
    EntityMemberInfo _hashedMember;
    Action<EntityRecord, EntityMemberInfo, object> _oldSetter; 

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      base.Apply(context, attribute, member);
      _member = member;
      _hashedMember = member.Entity.GetMember(this.PropertyName);
      if(string.IsNullOrEmpty(PropertyName)) {
        context.Log.Error("HashFor attribute - PropertyName must be specified. Entity/property: {0}.{1}.",
          member.Entity.Name, member.MemberName);
        return;
      }
      if(_member.DataType != typeof(int)) {
        context.Log.Error("HashFor attribute can be used only on int properties. Entity/property: {0}.{1}.",
          member.Entity.Name, member.MemberName);
        return;
      }
      if(_hashedMember == null) {
        context.Log.Error("Property {0} referenced in HashFor attribute on property {1} not found on entity {2}.", 
          PropertyName, member.MemberName, member.Entity.Name);
        return; 
      }
      if(_hashedMember.DataType != typeof(string)) {
        context.Log.Error("HashFor attribute on property {0}.{1}: target property must be of string type.",
          member.Entity.Name, member.MemberName);
        return; 
      }
      _oldSetter = _hashedMember.SetValueRef;
      _hashedMember.SetValueRef = OnSettingValue;
    }

    public void OnSettingValue(EntityRecord record, EntityMemberInfo member, object value) {
      _oldSetter(record, member, value);
      var strValue = (string)value;
      record.ValuesModified[_member.ValueIndex] = Util.StableHash(strValue); 
    }
  }

  public partial class OwnerAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.Flags |= EntityMemberFlags.IsOwner;
      if (member.Entity.OwnerMember == null)
        member.Entity.OwnerMember = member;
      else
        context.Log.Error("More than one Owner attribute is specified on entity {0}.", member.Entity.FullName);
    }

  }

  public partial class SecretAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.Flags |= EntityMemberFlags.Secret | EntityMemberFlags.NoDbUpdate; //updates are possible only thru custom update method
      member.GetValueRef = this.GetSecretValue;
    }

    private object GetSecretValue(EntityRecord record, EntityMemberInfo member) {
      //Always return value from Modified values - this value just set by the code; but no way to read value from database (which is in OriginalValues)
      var value = record.ValuesModified[member.ValueIndex];
      if (value == null)
        value = member.DeniedValue;
      if (value == DBNull.Value)
        return null;
      return value;
    }
  }

  public partial class IndexAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      if(string.IsNullOrWhiteSpace(this.MemberNames)) {
        context.Log.Error("Entity {0}: Index attribute on entity may not have empty member list.", entity.Name);
        return;
      }
    }
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      this.MemberNames = member.MemberName;
    }

  }//class

  public partial class DiscardOnAbortAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      entity.Flags |= EntityFlags.DiscardOnAbourt;
    }
  }

  public partial class DisplayAttribute : EntityModelAttributeBase {
    private MethodInfo _customMethodInfo;
    private string _adjustedFormat;
    private string[] _propNames;
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      if (this.MethodClass != null && this.MethodName != null) {
        _customMethodInfo = MethodClass.GetMethod(MethodName);
        if (_customMethodInfo == null) {
          context.Log.Error("Method {0} specified as Display method for entity {1} not found in type {2}", MethodName, entity.EntityType, MethodClass);
          return; 
        }
        entity.DisplayMethod = InvokeCustomDisplay;
        return; 
      }
      //Check if Format provided
      if (string.IsNullOrEmpty(Format)) {
        context.Log.Error("Invalid Display attribute on entity {0}. You must provide method reference or non-empty Format value.", entity.EntityType);
        return;
      }
      //Parse Format value, build argIndexes from referenced property names
      StringHelper.TryParseTemplate(Format, out _adjustedFormat, out _propNames); 
      //verify and build arg indexes
      foreach(var prop in _propNames) {
        //it might be dotted sequence of props; we check only first property
        var propSeq = prop.SplitNames('.');
        var member = entity.GetMember(propSeq[0]);
        if(member == null) 
          context.Log.Error("Invalid Format expression in Display attribute on entity {0}. Property {1} not found.", 
            entity.EntityType, propSeq[0]);
      }//foreach
      entity.DisplayMethod = GetDisplayString;
    }

    // we might have secure session, so elevate read to allow access
    private string InvokeCustomDisplay(EntityRecord record) {
      if (Disabled)
        return "(DisplayDisabled)";
      using (record.Session.ElevateRead()) {
        return (string)_customMethodInfo.Invoke(null, new object[] { record.EntityInstance });
      }
    }

    private string GetDisplayString(EntityRecord record) {
      if (Disabled)
        return "(DisplayDisabled)";
      if (_propNames == null) return Format; 
      var args = new object[_propNames.Length];
      for (int i = 0; i < args.Length; i++)
        args[i] = GetPropertyChainValue(record, _propNames[i]); 
      return StringHelper.SafeFormat(_adjustedFormat, args);
    }

    private object GetPropertyChainValue(EntityRecord record, string propertyChain) {
      using(record.Session.ElevateRead()) {
        if(!propertyChain.Contains('.'))
          return record.GetValue(propertyChain);
        var props = propertyChain.SplitNames('.');
        var currRec = record;
        object result = null;
        foreach(var prop in props) {
          result = currRec.GetValue(prop);
          if(result is EntityBase)
            currRec = EntityHelper.GetRecord(result);
          else
            return result; // stop as sson as we reach non-entity
        }
        return result;
      } //using
    } //method

    public static bool Disabled; //for debugging
  }//class

  public partial class CascadeDeleteAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      if (member.Kind != MemberKind.EntityRef) {
        context.Log.Error("CascadeDelete attribute may be used only on properties that are references to other entities. Property: {0}.{1}", 
            member.Entity.Name, member.MemberName);
        return;
      }
      member.Flags |= EntityMemberFlags.CascadeDelete;
    }
  }

  public partial class AsIsAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.Flags |= EntityMemberFlags.AsIs;
    }
  }

  public partial class BypassAuthorizationAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      entity.Flags |= EntityFlags.BypassAuthorization;
    }
  }

  public partial class GrantAccessAttribute : EntityModelAttributeBase {
    EntityMemberInfo _member;
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      if(member.Kind != MemberKind.EntityRef) {
        context.Log.Error(
          "GrantAccessTo attribute may be used only on properties that are references to other entities. Property: {0}.{1}",
            member.Entity.Name, member.MemberName);
        return;
      }
      _member = member; 
      //Delay until model construction must be completed - we need all member properties assigned to properly create member masks
      member.Entity.Module.App.AppEvents.Initializing += AppEvents_Initializing;
    }

    void AppEvents_Initializing(object sender, AppInitEventArgs e) {
      if(_member == null)
        return; 
      if(e.Step == EntityAppInitStep.EntityModelConstructed) {
        var targetEnt = _member.ReferenceInfo.ToKey.Entity;
        _member.ByRefPermissions = UserRecordPermission.Create(targetEnt, this.Properties, this.AccessType);
      }
    }

  }

  /* Not implemented yet
  public partial class DbComputedAttribute : EntityModelAttributeBase {
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      member.Flags |= EntityMemberFlags.DbComputed | EntityMemberFlags.NoDbInsert | EntityMemberFlags.NoDbUpdate;
      if(string.IsNullOrEmpty(SqlSpec))
        member.Flags |= EntityMemberFlags.AsIs;

    }
  }// class
   */ 


}//ns
