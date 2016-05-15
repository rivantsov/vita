using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime {

  public enum BoundListEventType {
    SavingChanges,
    SavedChanges,
    CanceledChanges,
  }

  public interface IPropertyBoundList {
    void Notify(BoundListEventType eventType);
    bool IsLoaded { get; }
    void Init(IList entities, IList linkEntities = null);
  }

  public abstract class PropertyBoundListBase<TEntity> : ObservableEntityList<TEntity>, IPropertyBoundList {
    public readonly EntityRecord OwnerRecord;
    public readonly EntityMemberInfo OwnerMember;
    public readonly EntityInfo TargetEntity; 

    protected PropertyBoundListBase(EntityRecord ownerRecord, EntityMemberInfo ownerMember) {
      OwnerRecord = ownerRecord;
      OwnerMember = ownerMember;
      TargetEntity = ownerMember.ChildListInfo.TargetEntity;
      IsOrdered = OwnerMember.ChildListInfo.PersistentOrderMember != null;
      // Note: it would be probably better to delay loading, but it is much more convenient in debugging with autoload on create.
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

    public abstract void Init(IList entities, IList linkEntities = null);
    #endregion
  }

}
