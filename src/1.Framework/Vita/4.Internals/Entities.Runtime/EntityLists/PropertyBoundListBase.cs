using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Utilities;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime {

  public enum BoundListEventType {
    SavingChanges,
    SavedChanges,
    CanceledChanges,
  }

  public interface IPropertyBoundList {
    bool IsLoaded { get; }
    void Notify(BoundListEventType eventType);
    void SetItems(object items);
    void SetAsEmpty();
  }

  internal abstract class PropertyBoundListBase<TEntity> : ObservableEntityList<TEntity>, IPropertyBoundList where TEntity: class {
    public readonly EntityRecord OwnerRecord;
    public readonly EntityMemberInfo OwnerMember;
    public readonly EntityInfo TargetEntity; 

    protected PropertyBoundListBase(EntityRecord ownerRecord, EntityMemberInfo ownerMember) {
      OwnerRecord = ownerRecord;
      OwnerMember = ownerMember;
      TargetEntity = ownerMember.ChildListInfo.TargetEntity;
      IsOrdered = OwnerMember.ChildListInfo.PersistentOrderMember != null;
      // Note: we delay loading, but it is convenient in debugging with autoload on create.
      // LoadList();
    }

    // If list changes to Modified, register it with session to get notified when we save changes
    public override bool Modified {
      get { return base.Modified;  }
      set {
        if (!base.Modified && value) {
          Util.Check(!OwnerRecord.Session.IsReadOnly, "Cannot modify entity list property in readonly session.");
          OwnerRecord.Session.ListsChanged.Add(this); 
        }
        base.Modified = value;
      }
    }
    public bool IsOrdered { get; protected set; }

    #region IPropertyBoundList Members
    public abstract void Notify(BoundListEventType eventType);
    // IsLoaded is inherited
    public abstract void SetItems(object items);
    public abstract void SetAsEmpty();
    #endregion
  }

}
