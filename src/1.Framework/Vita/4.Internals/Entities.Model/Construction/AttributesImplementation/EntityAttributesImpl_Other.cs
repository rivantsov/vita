using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using Vita.Entities.Utilities;

using Vita.Entities.Model;
using Vita.Entities.Model.Construction;
using Vita.Entities.Logging;
using Vita.Entities.Runtime;
using Vita.Entities.Services;

namespace Vita.Entities {
  // Special framework attributes, known to core framework and applied using specific internal logic

  public partial class EntityAttribute {
    public override void ApplyOnEntity(EntityModelBuilder builder) {
      if(!string.IsNullOrWhiteSpace(this.Name))
        HostEntity.Name = this.Name;
      HostEntity.TableName = this.TableName;
    }
  } //class

  public partial class NoColumnAttribute {
    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Kind = EntityMemberKind.Transient;
      HostMember.GetValueRef = MemberValueGettersSetters.GetTransientValue;
      HostMember.SetValueRef = MemberValueGettersSetters.SetTransientValue;
    }
  }// class

  public partial class ComputedAttribute {
    private MethodInfo _method;

    public override void Validate(ILog log) {
      base.Validate(log);
      if(HostMember.ClrMemberInfo.HasSetter())
        log.LogError($"Computed property {HostEntity.Name}.{HostMember.MemberName} may not have a setter, it is readonly.");
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      if(!this.Persist)
        HostMember.Kind = EntityMemberKind.Transient;
      HostMember.Flags |= EntityMemberFlags.Computed;
      _method = this.HostEntity.Module.FindFunction(this.MethodName, this.MethodClass);
      if(_method == null) {
        builder.Log.LogError($"Method {MethodName} for computed column {HostMember.MemberName} not found in type {this.MethodClass}");
        return;
      }
      HostMember.GetValueRef = GetComputedValue;
      HostMember.SetValueRef = MemberValueGettersSetters.DummySetValue;
    }

    public object GetComputedValue(EntityRecord rec, EntityMemberInfo member) {
      if(_method == null)
        return null;
      var value = _method.Invoke(null, new object[] { rec.EntityInstance });
      if(this.Persist && rec.Status != EntityStatus.Loaded)
        rec.SetValueDirect(member, value);
      return value;
    }

  }// class

  public partial class DbComputedAttribute {

    public override void Validate(ILog log) {
      base.Validate(log); 
      if (HostMember.ClrMemberInfo.HasSetter())
        log.LogError($"DbComputed member {HostMember.FullName} may not have a setter, it is readonly.");
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.ComputedKind = (DbComputedKindExt)(int)Kind;  
      HostMember.Flags |= EntityMemberFlags.DbComputed | EntityMemberFlags.NoDbInsert | EntityMemberFlags.NoDbUpdate;
    }

  }// class



  public partial class PersistOrderInAttribute {
    public override void Validate(ILog log) {
      base.Validate(log);
      if(HostMember.Kind != EntityMemberKind.EntityList)
        log.LogError($"PersistOrderIn attribute may be specified only on list members. Member: {HostRef}.");
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      EntityInfo orderedEntity = null;
      //determine the entity that is ordered
      switch(HostMember.ChildListInfo.RelationType) {
        case EntityRelationType.ManyToOne:
          orderedEntity = HostMember.ChildListInfo.TargetEntity;
          break;
        case EntityRelationType.ManyToMany:
          orderedEntity = HostMember.ChildListInfo.LinkEntity;
          break;
      }
      //check that there is a member 
      var orderMember = orderedEntity.GetMember(this.Property);
      if(orderMember == null) {
        builder.Log.LogError(
          $"Property '{Property}' referenced in PersistOrderIn attribute on {HostRef} not found in entity {orderedEntity.Name}.");
        return;
      }
      if(!orderMember.DataType.IsInt()) {
        builder.Log.LogError(
          $"Invalid data type ({orderMember.DataType}) in PersistOrderIn attribute on '{HostRef}' must be Int32.");
        return;
      }
      // Validation passed, assign order member
      HostMember.ChildListInfo.PersistentOrderMember = orderMember;
      // Make list order to be by orderMember
      var listInfo = HostMember.ChildListInfo;
      listInfo.OrderBy = new List<EntityKeyMemberInfo>();
      listInfo.OrderBy.Add(new EntityKeyMemberInfo(orderMember, desc: false));
    }
  }

  public partial class OrderByAttribute {

    public override void Validate(ILog log) {
      base.Validate(log);
      if(HostMember != null && HostMember.Kind != EntityMemberKind.EntityList) {
        log.LogError($"OrderBy attribute may be used only on entities or list properties. Property: {HostRef}");
      }
    }

    public override void ApplyOnEntity(EntityModelBuilder builder) {
      if(HostEntity.DefaultOrderBy != null) {
        builder.Log.LogError($"More than one OrderBy attribute in entity {HostRef}.");
      }
      if(!EntityModelBuilderExtensions.TryParseKeySpec(HostEntity, this.OrderByList, builder.Log, out HostEntity.DefaultOrderBy,
        ordered: true, specHolder: HostEntity))
        return;
      //Check that they are real cols
      foreach(var ordM in HostEntity.DefaultOrderBy) {
        if(ordM.Member.Kind != EntityMemberKind.Column)
          builder.Log.LogError($"Entity {HostEntity.Name} - Invalid property {ordM.Member.MemberName} " + 
            " in OrderBy attribute - must be a simple value column.");
      }
    }//method

    //This is a special case - OrderBy attribute specifies the order of entities in list property.
    public override void ApplyOnMember(EntityModelBuilder builder) {
      var entity = HostEntity;
      var listInfo = HostMember.ChildListInfo;
      EntityModelBuilderExtensions.TryParseKeySpec(HostMember.ChildListInfo.TargetEntity, this.OrderByList, builder.Log,
           out listInfo.OrderBy, ordered: true, specHolder: HostEntity);
    }


  }// class

  public partial class PropertyGroupAttribute {

    public override void Validate(ILog log) {
      base.Validate(log);
      if(string.IsNullOrWhiteSpace(this.GroupName))
        log.LogError($"Group name may not be empty. Entity: {HostRef}.");
    }

    public override void ApplyOnEntity(EntityModelBuilder builder) {
      var names = StringHelper.SplitNames(this.MemberNames);
      foreach(var name in names) {
        var member = HostEntity.GetMember(name);
        if(member == null) {
          builder.Log.LogError($"PropertyGroup '{GroupName}', entity {HostRef}: member {name} not found.");
          return;
        }
        var grp = HostEntity.GetPropertyGroup(this.GroupName, create: true);
        if(!grp.Members.Contains(member))
          grp.Members.Add(member);
      }//foreach
    }
  } //class

  public partial class GroupsAttribute {
    public override void Validate(ILog log) {
      base.Validate(log);
      if(string.IsNullOrWhiteSpace(this.GroupNames))
        log.LogError($"Groups value may not be empty, entity {HostRef}.");
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      var names = StringHelper.SplitNames(this.GroupNames);
      foreach(var name in names) {
        if(string.IsNullOrWhiteSpace(name))
          continue;
        var grp = HostEntity.GetPropertyGroup(name, create: true);
        if(!grp.Members.Contains(HostMember))
          grp.Members.Add(HostMember);
      }//foreach
    }
  } //class

  public partial class NoUpdateAttribute {
    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Flags |= EntityMemberFlags.NoDbUpdate;
    }
  }// class

  public partial class ReadOnlyAttribute {
    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Flags |= EntityMemberFlags.NoDbInsert | EntityMemberFlags.NoDbUpdate;
    }
  }// class

  public partial class DateOnlyAttribute {
    private Action<EntityRecord, EntityMemberInfo, object> _defaultSetter;

    public override void Validate(ILog log) {
      base.Validate(log);
      var dataType = HostMember.DataType;
      if(HostMember.DataType != typeof(DateTime) && HostMember.DataType != typeof(DateTime?))
        log.LogError($"Property {HostRef}: DateOnly attribute may be specified only on DataTime properties. ");
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      //Inject interceptor
      _defaultSetter = HostMember.SetValueRef;
      HostMember.SetValueRef = this.SetValueDateTime;
    }

    //Interceptors for SetValue
    void SetValueDateTime(EntityRecord record, EntityMemberInfo member, object value) {
      if(value != null && value != DBNull.Value) {
        if(value.GetType() == typeof(DateTime?))
          value = (DateTime?)((DateTime?)value).Value.Date;
        else
          value = ((DateTime)value).Date;
      }// if value != null
      _defaultSetter(record, member, value);
    }

  }// class

  public partial class UtcAttribute {
    Func<EntityRecord, EntityMemberInfo, object> _defaultGetter;
    Action<EntityRecord, EntityMemberInfo, object> _defaultSetter;

    public override void Validate(ILog log) {
      base.Validate(log);
      var dataType = HostMember.DataType;
      if(HostMember.DataType != typeof(DateTime) && HostMember.DataType != typeof(DateTime?))
        log.LogError($"Property {HostRef}: Utc attribute may be specified only on DataTime properties. ");
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Flags |= EntityMemberFlags.Utc;
      //Inject interceptor
      _defaultGetter = HostMember.GetValueRef;
      _defaultSetter = HostMember.SetValueRef;
      HostMember.GetValueRef = GetValueInterceptor;
      HostMember.SetValueRef = SetValueInterceptor;
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
      if(value == null || value == DBNull.Value)
        return value;
      DateTime dtValue;
      if(value.GetType() == typeof(DateTime?))
        dtValue = ((DateTime?)value).Value;
      else
        dtValue = (DateTime)value;
      // If value is coming from database or from serialization, its Kind shows Unspecified - set it to UTC explicitly; as we know that it is in fact UTC
      //if (record.Status == EntityStatus.Loading && dtValue.Kind == DateTimeKind.Unspecified)
      switch(dtValue.Kind) {
        case DateTimeKind.Utc:
          return dtValue;
        case DateTimeKind.Local:
          return dtValue.ToUniversalTime();
        case DateTimeKind.Unspecified:
          return DateTime.SpecifyKind(dtValue, DateTimeKind.Utc); // assume it is already UTC
        default:
          return dtValue; // just to supress compiler error
      }
    }
  }// class


  public partial class UnlimitedAttribute {
    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Flags |= EntityMemberFlags.UnlimitedSize;
      HostMember.Size = -1;
    }

  }// class


  public partial class DependsOnAttribute {
    public override void ApplyOnMember(EntityModelBuilder builder) {
      var namesArr = StringHelper.SplitNames(this.MemberNames);
      foreach(var name in namesArr) {
        var targetMember = HostEntity.GetMember(name);
        if(targetMember == null) {
          builder.Log.LogError($"Member {name} referenced in DependsOn attribute on member {HostRef} not found.");
          return;
        }
        //add this member to DependentMembers array of targetMember
        if(targetMember.DependentMembers == null)
          targetMember.DependentMembers = new EntityMemberInfo[] { HostMember };
        else {
          var mList = targetMember.DependentMembers.ToList();
          mList.Add(HostMember);
          targetMember.DependentMembers = mList.ToArray();
        }
      }//foreach name
    } // method
  }//class

  public partial class ValidateAttribute {
    MethodInfo _method;

    public override void ApplyOnEntity(EntityModelBuilder builder) {
      _method = this.MethodClass.GetMethod(this.MethodName);
      if(_method == null) {
        builder.Log.LogError($"Method {MethodName} specified as Validation method for entity {HostRef} not found in type {MethodClass}.");
        return;
      }
      HostEntity.Events.ValidatingChanges += Events_Validating;
    }

    void Events_Validating(EntityRecord record, EventArgs args) {
      _method.Invoke(null, new object[] { record.EntityInstance });
    }
  }// class

  public partial class PropagateUpdatedOnAttribute {
    Action<EntityRecord, EntityMemberInfo, object> _defaultValueSetter;

    public override void Validate(ILog log) {
      base.Validate(log);
      if(HostMember.Kind != EntityMemberKind.EntityRef)
        log.LogError(
          $"{this.GetType()} may be used only on properties that are references to other entities. Property: {HostRef}");
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      //Set interceptors
      // HostMember.Entity.Events.New += Events_New; // for new record, parent's status is updated when we set the parent ref in SetMemberValue method 
      HostMember.Entity.Events.Modified += Events_ModifiedDeleted;
      HostMember.Entity.Events.Deleting += Events_ModifiedDeleted;
      _defaultValueSetter = HostMember.SetValueRef;
      HostMember.SetValueRef = SetMemberValue;
    }

    private void Events_ModifiedDeleted(EntityRecord record, EventArgs args) {
      MarkTargetAsModified(record);
    }

    private void SetMemberValue(EntityRecord record, EntityMemberInfo member, object value) {
      _defaultValueSetter(record, member, value);
      MarkTargetAsModified(record);
    }
    private void MarkTargetAsModified(EntityRecord record) {
      var rec = record;
      var target = rec.GetValue(HostMember);
      if(target == null)
        return;
      var targetRec = EntityHelper.GetRecord(target);
      if(targetRec.Status == EntityStatus.Loaded)
        targetRec.Status = EntityStatus.Modified;
    }
  }


  public partial class HashForAttribute {
    EntityMemberInfo _hashedMember;
    Action<EntityRecord, EntityMemberInfo, object> _oldSetter;
    IHashingService _hashingService;

    public override void Validate(ILog log) {
      base.Validate(log);
      if(string.IsNullOrEmpty(this.PropertyName))
        log.LogError($"HashFor attribute - PropertyName must be specified. Entity/property: {HostRef}.");
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      if(HostMember.DataType != typeof(int)) {
        builder.Log.LogError($"HashFor attribute can be used only on int properties. Entity/property: {HostRef}.");
        return;
      }
      _hashedMember = HostEntity.GetMember(this.PropertyName);
      if(_hashedMember == null) {
        builder.Log.LogError($"Property {PropertyName} referenced in HashFor attribute on property {HostRef} not found.");
        return;
      }
      if(_hashedMember.DataType != typeof(string)) {
        builder.Log.LogError($"HashFor attribute on property {HostRef}: target property must be of string type.");
        return;
      }
      _oldSetter = _hashedMember.SetValueRef;
      _hashedMember.SetValueRef = OnSettingValue;
      _hashingService = builder.Model.App.GetService<IHashingService>();
    }

    public void OnSettingValue(EntityRecord record, EntityMemberInfo member, object value) {
      var rec = record;
      _oldSetter(rec, member, value);
      if(record.Status == EntityStatus.Loading)
        return; //we are loading from db 
      var strValue = (string)value;
      var hash = _hashingService.ComputeHash(strValue);
      rec.ValuesModified[HostMember.ValueIndex] = hash;
    }
  }

  public partial class SecretAttribute {
    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Flags |= EntityMemberFlags.Secret | EntityMemberFlags.NoDbUpdate; //updates are possible only thru custom update method
      HostMember.GetValueRef = GetSecretValue;
    }

    private object GetSecretValue(EntityRecord rec, EntityMemberInfo member) {
      var record = rec;
      //Always return value from Modified values - this value just set by the code; but no way to read value from database (which is in OriginalValues)
      var value = record.ValuesModified[member.ValueIndex];
      if(value == null)
        value = member.DeniedValue;
      if(value == DBNull.Value)
        return null;
      return value;
    }
  }

  public partial class DiscardOnAbortAttribute {
    public override void ApplyOnEntity(EntityModelBuilder builder) {
      HostEntity.Flags |= EntityFlags.DiscardOnAbourt;
    }
  }

  public partial class DisplayAttribute {
    private MethodInfo _customMethodInfo;
    private string _adjustedFormat;
    private string[] _propNames;

    public override void ApplyOnEntity(EntityModelBuilder builder) {
      if(this.MethodClass != null && this.MethodName != null) {
        _customMethodInfo = this.MethodClass.GetMethod(MethodName);
        if(_customMethodInfo == null) {
          builder.Log.LogError(
            $"Method {MethodName} specified as Display method for entity {HostRef} not found in type {MethodClass}");
          return;
        }
        HostEntity.DisplayMethod = InvokeCustomDisplay;
        return;
      }
      //Check if Format provided
      if(string.IsNullOrEmpty(this.Format)) {
        builder.Log.LogError(
          $"Invalid Display attribute on entity {HostRef}. You must provide method reference or non-empty Format value.");
        return;
      }
      //Parse Format value, build argIndexes from referenced property names
      StringHelper.TryParseTemplate(this.Format, out _adjustedFormat, out _propNames);
      //verify and build arg indexes
      foreach(var prop in _propNames) {
        //it might be dotted sequence of props; we check only first property
        var firstProp = prop.SplitNames('.')[0];
        var member = HostEntity.GetMember(firstProp);
        if(member == null) {
          builder.Log.LogError(
            $"Invalid Format expression in Display attribute on entity {HostRef}. Property {firstProp} not found.");
          return;
        }
      }//foreach
      HostEntity.DisplayMethod = GetDisplayString;
    }

    private string InvokeCustomDisplay(EntityRecord record) {
      if (record.Status == EntityStatus.Stub)
        return $"{record.EntityInfo.Name}/Unloaded";
      if(Disabled)
        return "(DisplayDisabled)";
      return (string)_customMethodInfo.Invoke(null, new object[] { record.EntityInstance });
    }

    private string GetDisplayString(EntityRecord record) {
      if(Disabled)
        return "(DisplayDisabled)";
      if(_propNames == null)
        return this.Format;
      var args = new object[_propNames.Length];
      for(int i = 0; i < args.Length; i++)
        args[i] = GetPropertyChainValue(record, _propNames[i]);
      return Util.SafeFormat(_adjustedFormat, args);
    }

    private object GetPropertyChainValue(EntityRecord record, string propertyChain) {
      var rec = record;
      using(rec.Session.ElevateRead()) {
        if(!propertyChain.Contains('.'))
          return rec.GetValue(propertyChain);
        var props = propertyChain.SplitNames('.');
        var currRec = rec;
        object result = null;
        foreach(var prop in props) {
          result = currRec.GetValue(prop);
          if(result is EntityBase)
            currRec = EntityHelper.GetRecord(result);
          else
            return result; // stop as soon as we reach non-entity
        }
        return result;
      } //using
    } //method

    public static bool Disabled; //for debugging
  }//class

  public partial class CascadeDeleteAttribute {

    public override void Validate(ILog log) {
      base.Validate(log);
      if(HostMember.Kind != EntityMemberKind.EntityRef)
        log.LogError(
          $"CascadeDelete attribute may be used only on properties that are references to other entities. Property: {HostRef}.");
    }
    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Flags |= EntityMemberFlags.CascadeDelete;
    }
  }

  public partial class AsIsAttribute {
    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Flags |= EntityMemberFlags.AsIs;
    }
  }

  public partial class BypassAuthorizationAttribute {
    public override void ApplyOnEntity(EntityModelBuilder builder) {
      HostEntity.Flags |= EntityFlags.BypassAuthorization;
    }
  }

  public partial class GrantAccessAttribute {
    public override void Validate(ILog log) {
      base.Validate(log);
      if(HostMember.Kind != EntityMemberKind.EntityRef)
        log.LogError(
          $"GrantAccess attribute may be used only on properties that are references to other entities. Property: {HostRef}");
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      var targetEnt = HostMember.ReferenceInfo.ToKey.Entity;
      // _member.ByRefPermissions = UserRecordPermission.Create(targetEnt, this.Properties, this.AccessType);
    }
  }

  public partial class ColumnAttribute {
    public override void ApplyOnMember(EntityModelBuilder builder) {
      if(!string.IsNullOrWhiteSpace(this.ColumnName))
        HostMember.ColumnName = this.ColumnName;
      if (!string.IsNullOrWhiteSpace(this.Default))
        HostMember.ColumnDefault = this.Default;
      HostMember.Scale = this.Scale;
      if (this.Precision > 0)
        HostMember.Precision = this.Precision;
      if (this.Size != 0)
        HostMember.Size = this.Size;
      HostMember.ExplicitDbTypeSpec = this.DbTypeSpec?.ToLowerInvariant();
    }

  }// class

  public partial class SizeAttribute {
    public override void ApplyOnMember(EntityModelBuilder builder) {
      if((this.Options & SizeOptions.AutoTrim) != 0)
        HostMember.Flags |= EntityMemberFlags.AutoTrim;
      // Check size code and lookup in tables
      if(!string.IsNullOrEmpty(this.SizeCode)) {
        var sizeTable = builder.Model.App.SizeTable;
        //If there is size code, look it up in SizeTable; first check module-specific value, then global value for the code
        int size;
        //check full code with module's namespace prefix or short size code
        var fullCode = Sizes.GetFullSizeCode(HostMember.Entity.EntityType.Namespace, this.SizeCode); 
        if (!sizeTable.TryGetValue(fullCode, out size) && !sizeTable.TryGetValue(this.SizeCode, out size)) {
          if (this.Size > 0)
            size = this.Size;
          else {
            builder.Log.LogError($"Size code '{SizeCode}' not found, entity member: {HostRef}");
            return;
          }
        } 
        HostMember.Size = size;
        return; 
      }
      //If size is specified explicitly, use it
      if(this.Size > 0) {
        HostMember.Size = this.Size;
        return;
      }
    }
  }// class

  public partial class NullableAttribute {

    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Flags |= EntityMemberFlags.Nullable;
      if (HostMember.DataType.IsValueType) {
        HostMember.Flags |= EntityMemberFlags.ReplaceDefaultWithNull;
        HostMember.GetValueRef = MemberValueGettersSetters.GetValueTypeReplaceNullWithDefault;
        HostMember.SetValueRef = MemberValueGettersSetters.SetValueTypeReplaceDefaultWithNull;
      }
    }
  }// class


  public partial class OldNamesAttribute {
    public override void ApplyOnEntity(EntityModelBuilder builder) {
      var names = StringHelper.SplitNames(this.OldNames);
      // add variation without leading 'I'
      var allNames = new List<string>(names);
      foreach(var n in names)
        if(n.Length > 1 && n.StartsWith("I"))
          allNames.Add(n.Substring(1));
      HostEntity.OldNames = allNames.ToArray(); 
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.OldNames = StringHelper.SplitNames(this.OldNames);
    }

  }// class


  public partial class OneToOneAttribute {
    EntityInfo _targetEntity;

    public override void Validate(ILog log) {
      // We set Kind to Transient to prevent treating the member as regular entity reference by model builder
      HostMember.Kind = EntityMemberKind.Transient;
      base.Validate(log);
    }

    public override void ApplyOnMember(EntityModelBuilder builder) {
      // It is initially assigned EntityRef
      if(!builder.Model.IsEntity(HostMember.DataType)) {
        builder.Log.LogError
          ($"{this.GetType()} may be used only on properties that reference other entities. Property: {HostRef}");
        return;
      }
      HostMember.Kind = EntityMemberKind.Transient;
      HostMember.Flags |= EntityMemberFlags.FromOneToOneRef;
      _targetEntity = builder.Model.GetEntityInfo(HostMember.DataType);
      Util.Check(_targetEntity != null, "Target entity not found: {0}", HostMember.DataType.Name);
      //check that PK of target entity points back to 'this' entity
      // limitations - no composte keys; we can use only primary keys for OneToOne
      var targetPkDataType = _targetEntity.PrimaryKey?.OwnerMember?.DataType;
      var isOk = targetPkDataType != null && targetPkDataType == HostEntity.EntityType;
      if(!isOk) {
        builder.Log.LogError(
          $"OneToOne property {HostRef}: target entity must have Primary key (non-composite entity ref) " + 
          $"that references back to the entity {HostEntity.EntityType}.");
        return;
      }
      HostMember.GetValueRef = GetValue;
      HostMember.SetValueRef = SetValue;
    }//method

    object GetValue(EntityRecord record, EntityMemberInfo member) {
      var v = record.GetRawValue(member);
      if(v != null) {
        if(v == DBNull.Value)
          return null;
        var rec = (EntityRecord)v;
        return rec.EntityInstance;
      }
      //retrieve entity 
      var ent = record.Session.SelectByPrimaryKey(_targetEntity, record.PrimaryKey.Values);
      if (ent == null) {
        record.SetValueDirect(member, DBNull.Value);
        return null;
      }
      var targetRec = ent.Record; 
      record.SetValueDirect(member, targetRec);
      return targetRec.EntityInstance;
    }

    void SetValue(EntityRecord record, EntityMemberInfo member, object value) {
      Util.Throw($"OneToOne properties are readonly, cannot set value. Property: {HostRef}");
    }
  }//attribute

  public partial class TransactionIdAttribute : EntityModelAttributeBase {
    private Guid? _defaultValue;

    public override void ApplyOnMember(EntityModelBuilder builder) {
      base.ApplyOnMember(builder);
      var member = base.HostMember;
      var type = Nullable.GetUnderlyingType(member.DataType) ?? member.DataType;
      if(type != typeof(long) && type != typeof(ulong)) {
        builder.Log.LogError($"TransactionId attribute may be used only on properties of type long or ulong.");
        return;
      }
      member.Flags |= EntityMemberFlags.IsSystem;
      _defaultValue = (member.DataType == typeof(Guid)) ? Guid.Empty : (Guid?)null;
      member.Entity.SaveEvents.SavingChanges += SaveEvents_SavingChanges;
    }

    void SaveEvents_SavingChanges(EntityRecord record, EventArgs args) {
      if(Action == TrackedAction.Created && record.Status == EntityStatus.New ||
          Action == TrackedAction.Updated && (record.Status == EntityStatus.New || record.Status == EntityStatus.Modified)) {
        //Do it directly, to bypass authorization checks (it should still work with record.SetValue)
        record.ValuesModified[HostMember.ValueIndex] = record.Session.GetNextTransactionId();
      }
    }//method

  }

}
