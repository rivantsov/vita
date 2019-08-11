using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Model.Construction;
using Vita.Entities.Runtime;
using Vita.Entities.Utilities;

namespace Vita.Entities {

  public partial class AutoAttribute {
    SequenceDefinition _sequence;

    public override void ApplyOnMember(EntityModelBuilder builder) {
      HostMember.Flags |= EntityMemberFlags.AutoValue;
      HostMember.AutoValueType = this.Type;
      switch (this.Type) {
        case AutoType.Identity:
          HostEntity.Flags |= EntityFlags.HasIdentity;
          HostEntity.IdentityMember = HostMember;
          base.HostMember.Flags |= EntityMemberFlags.Identity | EntityMemberFlags.NoDbInsert | EntityMemberFlags.NoDbUpdate;
          //Usually identity is int (or long). But there are some wierd real-life databases with Numeric (Decimal) identity columns
          // apparently MS SQL allows this
          var intOrDec = base.HostMember.DataType.IsInt() || base.HostMember.DataType == typeof(Decimal);
          if (!intOrDec) {
            builder.Log.LogError( $"Entity member {HostRef}, type {this.Type}: " + 
              " Identity attribute may be set only on member of integer or decimal types. ");
            return;
          }
          HostEntity.Events.New += EntityEvent_NewEntityHandleIdentity;
          HostEntity.SaveEvents.SubmittedChanges += EntityEvent_IdentityEntitySubmitted;
          break;

        case AutoType.Sequence:
          if (!base.HostMember.DataType.IsInt()) {
            builder.Log.LogError($"Entity member {HostRef}, type {this.Type}: " + 
              "Sequence attribute may be set only on member of integer types. ");
            return;
          }
          if (string.IsNullOrWhiteSpace(this.SequenceName)) {
            builder.Log.LogError($"Entity member {HostRef}: Sequence name must be specified.");
            return;
          }
          _sequence = builder.Model.FindSequence(this.SequenceName, HostEntity.Module);
          if (_sequence == null) {
            builder.Log.LogError($"Entity member {HostRef}: Sequence {this.SequenceName} not defined.");
            return;
          }
          if (_sequence.DataType != base.HostMember.DataType) {
            builder.Log.LogError($"Entity member {HostRef}: " + 
              $" data type {HostMember.DataType} does not match sequence '{this.SequenceName}' data type {_sequence.DataType}.");
            return;
          }
          HostEntity.Events.New += EntityEvent_NewEntityHandleSequence;
          break;

        case AutoType.NewGuid:
          if (!CheckDataType(builder, typeof(Guid))) {
            builder.Log.LogError($"Entity member {HostRef}: " + 
              " Auto attribute with AutoType=NewGuid must be on Guid-type member.");
            return;
          }
          HostEntity.Events.New += EntityEvent_HandleNewGuid;
          break;

        case AutoType.CreatedOn:
        case AutoType.UpdatedOn:
          if (!CheckDataType(builder, typeof(DateTime), typeof(DateTime?)))
            return;
          if (this.Type == AutoType.CreatedOn)
            base.HostMember.Flags |= EntityMemberFlags.NoDbUpdate;
          HostEntity.SaveEvents.SavingChanges += EntityEvent_HandleCreatedUpdatedOnDateTime;
          break;

        case AutoType.CreatedBy:
          if (!CheckDataType(builder, typeof(string)))
            return;
          HostEntity.Events.New += EntityEvent_HandleUpdatedCreatedBy;
          base.HostMember.Flags |= EntityMemberFlags.NoDbUpdate;
          break;

        case AutoType.UpdatedBy:
          if (!CheckDataType(builder, typeof(string)))
            return;
          HostEntity.Events.New += EntityEvent_HandleUpdatedCreatedBy;
          HostEntity.Events.Modified += EntityEvent_HandleUpdatedCreatedBy;
          break;

        case AutoType.CreatedById:
          if (!CheckDataType(builder, typeof(Guid), typeof(int)))
            return;
          HostEntity.Events.New += EntityEvent_HandleUpdatedCreatedById;
          base.HostMember.Flags |= EntityMemberFlags.NoDbUpdate;
          break;

        case AutoType.UpdatedById:
          if (!CheckDataType(builder, typeof(Guid), typeof(int)))
            return;
          HostEntity.Events.New += EntityEvent_HandleUpdatedCreatedById;
          HostEntity.Events.Modified += EntityEvent_HandleUpdatedCreatedById;
          break;

        case AutoType.TransIdCreated:
          if (!CheckDataType(builder, typeof(long), typeof(ulong)))
            return;
          HostEntity.Events.New += HandleCreatedUpdatedInTransId;
          HostMember.Flags |= EntityMemberFlags.NoDbUpdate;
          break;

        case AutoType.TransIdUpdated:
          if (!CheckDataType(builder, typeof(long), typeof(ulong)))
            return;
          HostEntity.Events.New += HandleCreatedUpdatedInTransId;
          HostEntity.Events.Modified += HandleCreatedUpdatedInTransId;
          break;

        case AutoType.RowVersion:
          HostEntity.RowVersionMember = base.HostMember;
          base.HostMember.Flags |= EntityMemberFlags.RowVersion | EntityMemberFlags.NoDbInsert | EntityMemberFlags.NoDbUpdate;
          base.HostEntity.Flags |= EntityFlags.HasRowVersion;
          base.HostMember.ExplicitDbTypeSpec = "rowversion";
          break;
      }//swith AutoValueType
    }


    private bool CheckDataType(EntityModelBuilder builder, params Type[] types) {
      var dt = HostMember.DataType;
      if (dt.IsGenericType)
        dt = dt.GetGenericArguments()[0]; // check for DateTime?
      if (types.Contains(dt))
        return true;
      var okTypes = string.Join(", ", types.Select(t => t.Name));
      builder.Log.LogError($"Entity member {HostRef}: Auto({this.Type})  attribute may be set only on member of type(s): {okTypes}. ");
      return false;
    }

    /* We initialize identity fields with temp negative values; when record is saved, they will be replaced with generated identity
     * The reason for temp negative values is to make entity comparison work properly for unsaved entities
     */
    private long _identityCount; //used for temp values in identity attributes
    void EntityEvent_NewEntityHandleIdentity(EntityRecord record, EventArgs args) {
      var rec = record;
      if (rec.SuppressAutoValues)
        return;
      var newIdValue = System.Threading.Interlocked.Decrement(ref _identityCount);
      var initValue = Convert.ChangeType(newIdValue, HostMember.DataType);
      rec.SetValueDirect(HostMember, initValue); //0, Int32 or Int64
    }

    void EntityEvent_NewEntityHandleSequence(EntityRecord record, EventArgs args) {
      var rec = record;
      if (rec.SuppressAutoValues)
        return;
      object value;
      value = rec.Session.GetSequenceNextValue(_sequence);
      if (_sequence.DataType == typeof(int))
        value = (int)(long)value;
      rec.SetValueDirect(HostMember, value);
    }

    void EntityEvent_IdentityEntitySubmitted(EntityRecord record, EventArgs args) {
      var rec = record;
      if (rec.Status != EntityStatus.New)
        return;
      var recs = rec.Session.RecordsChanged;
      foreach (var refMember in rec.EntityInfo.IncomingReferences) {
        var childRecs = recs.Where(r => r.EntityInfo == refMember.Entity);
        foreach (var childRec in childRecs) {
          var refBackValue = childRec.ValuesTransient[refMember.ValueIndex];
          if (refBackValue == record)
            refMember.SetValueRef(childRec, refMember, rec.EntityInstance); //this will copy foreign key
        }//foreach childRec
      }
    }//method


    void EntityEvent_HandleNewGuid(EntityRecord record, EventArgs args) {
      var rec = record;
      if (rec.SuppressAutoValues)
        return;
      var newGuid = Guid.NewGuid();
      rec.SetValueDirect(HostMember, newGuid);
    }

    void EntityEvent_HandleUpdatedCreatedBy(EntityRecord record, EventArgs args) {
      var rec = record;
      if (rec.SuppressAutoValues)
        return;
      var userName = record.Session.Context.User.UserName;
      rec.SetValueDirect(HostMember, userName);
    }

    void EntityEvent_HandleUpdatedCreatedById(EntityRecord record, EventArgs args) {
      var rec = record;
      if (rec.SuppressAutoValues)
        return;
      // Application might be using either Guids for UserIDs or Ints (int32 or Int64);
      // take this into account
      if (HostMember.DataType == typeof(Guid)) {
        var userId = record.Session.Context.User.UserId;
        rec.SetValueDirect(HostMember, userId);
      } else {
        //UserID is Int (identity); use AltUserId
        var altUserId = record.Session.Context.User.AltUserId;
        object userId = HostMember.DataType == typeof(Int64) ? altUserId : Convert.ChangeType(altUserId, HostMember.DataType);
        rec.SetValueDirect(HostMember, userId);
      }
    }
    private void HandleCreatedUpdatedInTransId(EntityRecord record, EventArgs args) {
      record.SetValueDirect(HostMember, record.Session.GetNextTransactionId());
    }

    void EntityEvent_HandleCreatedUpdatedOnDateTime(EntityRecord rec, EventArgs args) {
      var record = rec;
      if (record.SuppressAutoValues)
        return;
      //Do CreatedOn only if it is new record
      if (this.Type == AutoType.CreatedOn && record.Status != EntityStatus.New)
        return;
      var dtTransStart = record.Session.LastTransactionDateTime.Value;
      if (HostMember.Flags.IsSet(EntityMemberFlags.Utc))
        dtTransStart = dtTransStart.ToUniversalTime();
      record.SetValueDirect(HostMember, dtTransStart);
    }

  }// class


}
