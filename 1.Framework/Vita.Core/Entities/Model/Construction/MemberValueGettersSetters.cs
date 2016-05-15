using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Runtime;

namespace Vita.Entities.Model.Construction {

  // The convention for storing values in OriginalValues and ModifiedValues arrays:
  //  1. CLR "null" value in the array means "was not assigned" - it is an indication that record should be (re-)loaded; 
  //        DbNull.Value identifies "null" value (database NULL), so GetValue function reads value from array, and if it is
  //        DbNull.Value, it return either CLR null (for strings), or default(type) for value types
  //  2. When a record is loaded from database (Status==Loading), values are put into OriginalValues; for all other statuses 
  //     the values are put into ModifiedValues; ModifiedValues initially contains nulls, meaning "not-assigned", so 
  //        "ModifiedValues[i] == null" means value was not assigned since Status changed to Loaded;
  //       then we should get value from OriginalValues[i]
  //  3. FK links handling; we create the following members: 
  //         1) the referenced entity itself - this property in on Entity's interface
  //         2) all foreign key fields for the entity - these are plain ValueType-fields present in Record's data arrays
  //      This requires certain syncronization to occur: when we set the Entity property, 
  //       we must update values of foreign keys. When we change foreign key value, we must update 
  //       the Entity property; Because FK keys might be composite, setting the key happens in 
  //       several steps, so we basically don't know when the FK is fully set and we can lookup the entity.
  //       We set entity reference to null (meaning not-set) when any FK field is assigned,
  //       so entity must be looked up/reset again

  public static class MemberValueGettersSetters {
    //Assigns default Get/Set handlers
    public static void AssignDefaultGetSetHandlers(EntityMemberInfo member) {
      switch (member.Kind) {
        case MemberKind.Column:
          member.GetValueRef = GetSimpleValue;
          member.SetValueRef = SetSimpleValue;
          //Special case for enum. If property is Nullable<enum>, and value coming from Db is int, then auto conversion does not work, we have to do it explicitly
          if (member.DataType.IsNullableValueType()) {
            var baseType = Nullable.GetUnderlyingType(member.DataType);
            if (baseType.IsEnum)
              member.GetValueRef = GetNullableEnumValue;
          }
          break;
        case MemberKind.EntityRef:
          member.GetValueRef = GetEntityRefValue;
          member.SetValueRef = SetEntityRefValue;
          break;
        case MemberKind.EntityList:
          member.GetValueRef = GetEntityListValue;
          member.SetValueRef = DummySetValue;
          break; 
      }//switch
    }


    //Helper methods, various implementations of GetValue, SetValue handlers, for different member kinds
    #region Dummy methods
    public static object DummyGetValue(EntityRecord record, EntityMemberInfo member) {
      return null; 
    }
    public static void DummySetValue(EntityRecord record, EntityMemberInfo member, object value) {
    }
    #endregion

    #region Get/Set column value - plain persistent in database table
    public static object GetSimpleValue(EntityRecord record, EntityMemberInfo member) {
      var value = record.GetValueDirect(member);
      if (value == null && record.Status != EntityStatus.New) {
        record.Reload();
        value = record.GetValueDirect(member);
      }
      if (value == DBNull.Value)
        return null;
      return value; 
    }

    public static void SetSimpleValue(EntityRecord record, EntityMemberInfo member, object value) {
      if (value == null)
        value = DBNull.Value;
      var oldValue = record.GetValueDirect(member);
      if (member.AreValuesEqual(oldValue, value))
        return;
      record.SetValueDirect(member, value);
      if (record.Status == EntityStatus.Loaded)
        record.Status = EntityStatus.Modified;
    }

    //Special case for enum. If property is Nullable<enum>, and value coming from Db is int, then auto conversion does not work, we have to do it explicitly
    public static object GetNullableEnumValue(EntityRecord record, EntityMemberInfo member) {
      var value = GetSimpleValue(record, member);
      if (value == DBNull.Value || value == null)
        return value;
      if (!value.GetType().IsEnum) 
        value = Enum.ToObject(Nullable.GetUnderlyingType(member.DataType), value);
      return value; 

    }
    #endregion

    #region Get/Set simple no-column value
    public static object GetTransientValue(EntityRecord record, EntityMemberInfo member) {
      return record.ValuesTransient[member.ValueIndex];
    }

    public static void SetTransientValue(EntityRecord record, EntityMemberInfo member, object value) {
      record.ValuesTransient[member.ValueIndex] = value; 
    }
    #endregion


    #region Nullable value types
    // The property on interface is not nullable (int), but the column in database is. We substitute the default value for type (0)
    // with DbNull.Value
    public static object GetValueTypeReplaceNullWithDefault(EntityRecord record, EntityMemberInfo member) {
      var value = GetSimpleValue(record, member);
      if (value == DBNull.Value)
        return member.DefaultValue;
      return value; 
    }

    public static void SetValueTypeReplaceDefaultWithNull(EntityRecord record, EntityMemberInfo member, object value) {
      if (value == member.DefaultValue)
        value = DBNull.Value;
      SetSimpleValue(record, member, value);
    }
    #endregion

    #region Entity reference value 
    // When we get the value, if it is null, we must try to load the target using foreign key values
    // When we set the value, we need to copy primary key values into foreign key members
    public static object GetEntityRefValue(EntityRecord record, EntityMemberInfo member) {
      var value = record.ValuesTransient[member.ValueIndex];
      if (value == DBNull.Value)
        return null;
      if (value != null) {
        //Check if record is not fantom - might happen for cached records
        var oldRecRef = (EntityRecord) value; 
        if (oldRecRef.Status != EntityStatus.Fantom)
          return oldRecRef.EntityInstance;
      }
      var rec = GetEntityRefTarget(record, member);
      if (rec == null) {
        record.ValuesTransient[member.ValueIndex] = DBNull.Value; 
        return null;
      } 
      // rec != null
      if (rec.ByRefUserPermissions == null)
        rec.ByRefUserPermissions = member.ReferenceInfo.ByRefPermissions;
      record.ValuesTransient[member.ValueIndex] = rec;
      return rec.EntityInstance;
    }

    public static void SetEntityRefValue(EntityRecord record, EntityMemberInfo member, object value) {
      //If there's list on the other side, mark target records( old ref and new ref) to clear lists.
      if(member.ReferenceInfo.TargetListMember != null)
        MarkTargetToClearLists(record, member, value);
      EntityRecord newRec = null;
      if (value == null)
        value = DBNull.Value;
      if (value == DBNull.Value) {
        record.ValuesTransient[member.ValueIndex] = DBNull.Value;
      } else {
        newRec = EntityHelper.GetRecord(value);
        Util.Check(newRec != null, "Invalid entity ref value - not an entity: {0}", value);
        record.ValuesTransient[member.ValueIndex] = newRec;
        if(newRec.ByRefUserPermissions == null)
          newRec.ByRefUserPermissions = member.ReferenceInfo.ByRefPermissions;
      }
      CopyPrimaryKeyToForeign(record, member, newRec);
      if (record.Status == EntityStatus.Loaded)
        record.Status = EntityStatus.Modified;
    }

    //Utilities
    private static void MarkTargetToClearLists(EntityRecord record, EntityMemberInfo member, object newEntityRef) {
      //If record is not new, mark old ref to clear lists
      if (record.Status != EntityStatus.New) {
        EntityRecord oldTargetRec;
        var oldTarget = record.ValuesTransient[member.ValueIndex];
        if(oldTarget == null)
          oldTargetRec = GetEntityRefTarget(record, member);
        else 
          oldTargetRec = EntityHelper.GetRecord(oldTarget);
        if(oldTargetRec != null)
          oldTargetRec.MarkForClearLists();
      }
      //Check new ref
      if(newEntityRef != null) {
        var newTargetRec = EntityHelper.GetRecord(newEntityRef);
        if(newTargetRec != null)
          newTargetRec.MarkForClearLists(); 
      }
    }//method

    public static EntityRecord GetEntityRefTarget(EntityRecord record, EntityMemberInfo member) {
      // Current value is null (meaning not set, yet). We must figure it out. 
      // 1. If corresponding FK is null, then value is DbNull.
      var fromKey = member.ReferenceInfo.FromKey;
      var toKey = member.ReferenceInfo.ToKey;
      if (!record.KeyIsLoaded(fromKey) && record.Status != EntityStatus.New)
        record.Reload();
      if (record.KeyIsNull(fromKey))
        return null;
      // 2. The key is not null, let's lookup the record by FK; maybe it was already loaded
      var fkValue = new EntityKey(fromKey, record);
      var targetEntity = toKey.Entity;
      //Compute pkValue on target entity
      var pkValue = new EntityKey(targetEntity.PrimaryKey, fkValue.Values);
      //If Session available, ask session for existing record
      EntityRecord targetRec;
      if (record.Session != null)
        targetRec = record.Session.GetRecord(pkValue, LoadFlags.Stub); //either already loaded or a stub
      else
        targetRec = new EntityRecord(pkValue); //create detached stub
      return targetRec;
    }//method

    //Copies PK values into corresponding FK
    public static void CopyPrimaryKeyToForeign(EntityRecord record, EntityMemberInfo entityRefMember, EntityRecord refTarget) {
      var refInfo = entityRefMember.ReferenceInfo;
      var fkMembers = refInfo.FromKey.ExpandedKeyMembers;
      //If target is null, set all to DbNull 
      if (refTarget == null) {
        for (int i = 0; i < fkMembers.Count; i++)
          record.SetValueDirect(fkMembers[i].Member, DBNull.Value);
        return;
      }
      //refTarget is not null
      var pkMembers = refInfo.ToKey.ExpandedKeyMembers;
      for (int i = 0; i < pkMembers.Count; i++) {
        //copy value from PK to FK member
        var value = refTarget.GetValueDirect(pkMembers[i].Member); 
        record.SetValueDirect(fkMembers[i].Member, value); 
      }
    }//method

    #endregion

    #region Entity list 
    // When we get the value, if it is null, we must try to load the list
    public static object GetEntityListValue(EntityRecord record, EntityMemberInfo member) {
      var list = record.ValuesTransient[member.ValueIndex];
      if (list != null)
        return list;
      //create new list
      list = record.InitChildEntityList(member);
      return list;    
    }
    #endregion


    // Default implementation of comparer
    public static bool AreObjectsEqual(object x, object y) {
      if (x == null)
        return y == null;
      if (y == null)
        return false;
      return x.Equals(y);
    }
    

  }
}
