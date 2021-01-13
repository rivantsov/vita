using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities {
  // Mostly for use by Entity Attributes for making run-time hooks.
  //The event itself is on EntityInfo; the event handler recieves the notification about an event in entity entity
  // with source=entity. Entity itself is available in entity.EntityInstance property
  // EntityApp has also EntityEvents property, so app can signup to entity events at app level
  public delegate void EntityEventHandler(EntityRecord record, EventArgs args);

  //Set of events associated with EntityInfo
  public class EntityEvents {
    public event EntityEventHandler New;          
    public event EntityEventHandler Loaded;       
    public event EntityEventHandler Deleting;     
    public event EntityEventHandler Modified;     
    public event EntityEventHandler ChangesCanceled;

    public event EntityEventHandler ValidatingChanges;
    public event EntityEventHandler ValidationFailed;

    internal void OnNew(EntityRecord record) {
      New?.Invoke(record, EventArgs.Empty);
    }
    internal void OnLoaded(EntityRecord record) {
      Loaded?.Invoke(record, EventArgs.Empty);
    }
    internal void OnDeleting(EntityRecord record) {
      Deleting?.Invoke(record, EventArgs.Empty);
    }
    internal void OnModified(EntityRecord record) {
      Modified?.Invoke(record, EventArgs.Empty);
    }
    internal void OnValidatingChanges(EntityRecord record) {
      ValidatingChanges?.Invoke(record, EventArgs.Empty);
    }

    internal void OnValidationFailed(EntityRecord record) {
      ValidationFailed?.Invoke(record, EventArgs.Empty);
    }
    internal void OnChangesCanceled(EntityRecord record) {
      ChangesCanceled?.Invoke(record, EventArgs.Empty);
    }
  }

  public class EntitySaveEvents {
    public event EntityEventHandler SavingChanges;
    public event EntityEventHandler SavedChanges;  //after all changes have been submitted
    public event EntityEventHandler SaveChangesAborted;  //after all changes have been submitted
    // right after database command had been executed. Used for handling identity columns in new entitys - the handler copies the identity value
    // to all child entitys
    public event EntityEventHandler SubmittedChanges;  


    internal void OnSavingChanges(EntityRecord record) {
      SavingChanges?.Invoke(record, EventArgs.Empty);
    }
    internal void OnSavedChanges(EntityRecord record) {
      SavedChanges?.Invoke(record, EventArgs.Empty);
    }
    internal void OnSaveChangesAborted(EntityRecord record) {
      SaveChangesAborted?.Invoke(record, EventArgs.Empty);
    }
    //called from Database, that's why it is public
    public void OnSubmittedChanges(EntityRecord record) {
      SubmittedChanges?.Invoke(record, EventArgs.Empty);
    }
  }

}//ns
