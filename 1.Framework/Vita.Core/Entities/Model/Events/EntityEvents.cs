using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Runtime;

namespace Vita.Entities.Model {
  // Mostly for use by Entity Attributes for making run-time hooks.
  //The event itself is on EntityInfo; the event handler recieves the notification about an event in entity record
  // with source=record. Entity itself is available in record.EntityInstance property
  // EntityApp has also EntityEvents property, so app can signup to entity events at app level
  public delegate void EntityEventHandler(EntityRecord record, EventArgs args);

  //Set of events associated with EntityInfo
  public class EntityEvents {
    public event EntityEventHandler New;          // Fired in Session.NewRecord method
    public event EntityEventHandler Loaded;       // Fired in EntityRecord.GetRecord, Record.Reload, Session.NotifyRecordStatusChanged
    public event EntityEventHandler Deleting;     // Fired in EntityRecord.MarkForDelete
    public event EntityEventHandler Modified;     // Fired in EntityRecord.SetValue
    public event EntityEventHandler ChangesCanceled;

    public event EntityEventHandler ValidatingChanges;
    public event EntityEventHandler ValidationFailed;

    internal void OnNew(EntityRecord record) {
      if (New != null) 
        New(record, EventArgs.Empty);
    }
    internal void OnLoaded(EntityRecord record) {
      if (Loaded != null)
        Loaded(record, EventArgs.Empty);
    }
    internal void OnDeleting(EntityRecord record) {
      if (Deleting != null)
        Deleting(record, EventArgs.Empty);
    }
    internal void OnModified(EntityRecord record) {
      if (Modified != null)
        Modified(record, EventArgs.Empty);
    }
    internal void OnValidatingChanges(EntityRecord record) {
      if (ValidatingChanges != null)
        ValidatingChanges(record, EventArgs.Empty);
    }

    internal void OnValidationFailed(EntityRecord record) {
      if (ValidationFailed != null)
        ValidationFailed(record, EventArgs.Empty);
    }
    internal void OnChangesCanceled(EntityRecord record) {
      if (ChangesCanceled != null)
        ChangesCanceled(record, EventArgs.Empty);
    }
  }

  public class EntitySaveEvents {
    public event EntityEventHandler SavingChanges;
    public event EntityEventHandler SavedChanges;  //after all changes have been submitted
    public event EntityEventHandler SaveChangesAborted;  //after all changes have been submitted
    // right after database command had been executed. Used for handling identity columns in new records - the handler copies the identity value
    // to all child records
    public event EntityEventHandler SubmittedChanges;  


    internal void OnSavingChanges(EntityRecord record) {
      if (SavingChanges != null)
        SavingChanges(record, EventArgs.Empty);
    }
    internal void OnSavedChanges(EntityRecord record) {
      if (SavedChanges != null)
        SavedChanges(record, EventArgs.Empty);
    }
    internal void OnSaveChangesAborted(EntityRecord record) {
      if (SaveChangesAborted != null)
        SaveChangesAborted(record, EventArgs.Empty);
    }
    //called from Database, that's why it is public
    public void OnSubmittedChanges(EntityRecord record) {
      if (SubmittedChanges != null)
        SubmittedChanges(record, EventArgs.Empty);
    }
  }

}//ns
