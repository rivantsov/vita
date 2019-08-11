using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Services;

namespace Vita.Modules.Logging {
  using Vita.Entities.Model.Construction;

  public enum TrackingActionType {
    Created,
    Updated,
    Deactivated, //not used, might be used in the future
  }


  /// <summary>An attribute to use on Guid properties that will hold the ID of the user transaction that created or 
  /// was last to modify an entity. 
  /// </summary>
  /// <remarks>It is a common practice to add extra tracking columns like [CreatedByUser], [UpdatedByUser], 
  /// [CreatedOnDateTime] and alike to tables in the database to track the create/update actions.
  /// VITA provides an alternative - instead of adding these multiple string/date-time columns, you can track user
  /// transactions (update actions) in a separate table [UserTransaction], and add references (ID values) to table 
  /// you need to track. Add properties like CreatedInId, UpdatedInId (Guid) to entities you want to track and decorate
  /// the properties with the [Track(actiontype)] attribute.
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public class TrackAttribute : EntityModelAttributeBase {
    public TrackingActionType ActionType;

    public TrackAttribute(TrackingActionType actionType = TrackingActionType.Updated) {
      ActionType = actionType;
    }

    private Guid? _defaultValue;
    public override void ApplyOnMember(EntityModelBuilder builder) {
      base.ApplyOnMember(builder);
      if (HostMember.DataType != typeof(Guid) && HostMember.DataType != typeof(Guid?)) {
        builder.Log.Error("Track attribute may be used only on Guid properties.");
        return;
      }
      HostMember.Flags |= EntityMemberFlags.IsSystem;
      _defaultValue = (HostMember.DataType == typeof(Guid)) ? Guid.Empty : (Guid?)null;
      HostMember.Entity.SaveEvents.SavingChanges += SaveEvents_SavingChanges;
    }

    void SaveEvents_SavingChanges(EntityRecord record, EventArgs args) {
      if(ActionType == TrackingActionType.Created && record.Status == EntityStatus.New ||
          ActionType == TrackingActionType.Updated && (record.Status == EntityStatus.New || record.Status == EntityStatus.Modified)) {
        //Do it directly, to bypass authorization checks (it should still work with record.SetValue)
        record.ValuesModified[HostMember.ValueIndex] = record.Session.NextTransactionId;
      }
    }//method

  }//class

}
