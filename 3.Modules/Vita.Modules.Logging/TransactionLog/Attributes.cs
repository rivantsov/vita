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

    private EntityMemberInfo _member;
    private Guid? _defaultValue;

    public override void Apply(Entities.Model.Construction.AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      base.Apply(context, attribute, member);
      _member = member;
      if(member.DataType != typeof(Guid) && member.DataType != typeof(Guid?)) {
        context.Log.Error("ActivityTrack attribute may be used only on Guid properties.");
        return;
      }
      _member.Flags |= EntityMemberFlags.IsSystem;
      _defaultValue = (_member.DataType == typeof(Guid)) ? Guid.Empty : (Guid?)null;
      member.Entity.SaveEvents.SavingChanges += SaveEvents_SavingChanges;
    }

    void SaveEvents_SavingChanges(EntityRecord record, EventArgs args) {
      if(ActionType == TrackingActionType.Created && record.Status == EntityStatus.New ||
          ActionType == TrackingActionType.Updated && (record.Status == EntityStatus.New || record.Status == EntityStatus.Modified)) {
        //Do it directly, to bypass authorization checks (it should still work with record.SetValue)
        record.ValuesModified[_member.ValueIndex] = record.Session.NextTransactionId;
      }
    }//method

  }//class

  /// <summary>Marks an entity as not tracked in Transaction log. Explicitly instructs the system to not track 
  /// an entity, even if it belongs to one of the entity areas being tracked.</summary>
  [AttributeUsage(AttributeTargets.Interface)]
  public class DoNotTrack : EntityModelAttributeBase {
    public override void Apply(Entities.Model.Construction.AttributeContext context, Attribute attribute, EntityInfo entity) {
      base.Apply(context, attribute, entity);
      entity.Flags |= EntityFlags.DoNotTrack;
    }

  }


}
