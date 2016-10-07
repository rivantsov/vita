using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Linq;
using Vita.Entities.Logging;
using Vita.Entities.Runtime;

namespace Vita.Entities.Model {

  public enum EntityAppInitStep {
    Initializing,
    EntityModelConstructed,
    Initialized,
  }


  public class AppInitEventArgs : EventArgs {
    public readonly EntityApp App;
    public readonly MemoryLog Log;
    public readonly EntityAppInitStep Step;

    public AppInitEventArgs(EntityApp app, MemoryLog log, EntityAppInitStep step) {
      App = app;
      Log = log; 
      Step = step; 
    }
  }

  public class ServiceEventArgs : EventArgs {
    public readonly EntityApp App; 
    public readonly Type ServiceType;
    public readonly object ServiceInstance;
    public ServiceEventArgs(EntityApp app, Type serviceType, object serviceInstance) {
      App = app; 
      ServiceType = serviceType;
      ServiceInstance = serviceInstance; 
    }
  }

  public class EntitySessionEventArgs : EventArgs {
    public readonly IEntitySession Session;
    public EntitySessionEventArgs(IEntitySession session) {
      Session = session; 
    }
  }
  public class LinqCommandEventArgs : EntitySessionEventArgs {
    public readonly LinqCommand Command; 
    public LinqCommandEventArgs(IEntitySession session, LinqCommand command) : base(session) {
      Command = command; 
    }
  }
  public class EntityCommandEventArgs : EntitySessionEventArgs {
    public readonly EntityCommand Command;
    public EntityCommandEventArgs(IEntitySession session, EntityCommand command)
      : base(session) {
      Command = command;
    }
  }

  public class AppErrorEventArgs : EventArgs {
    public readonly IEntitySession Session;
    public readonly Exception Exception;
    public AppErrorEventArgs(IEntitySession session, Exception exception) {
      Session = session; 
      Exception = exception;
    }
  }//class
  
  public delegate void AppEventHandler<TArgs>(EntityApp app, TArgs args) where TArgs: EventArgs;

  //Events for the entire entity store
  public class EntityAppEvents {
    EntityApp _app; 

    public event EventHandler<AppInitEventArgs> Initializing; //fired multiple times
    public event EventHandler FlushRequested;
    public event EventHandler<ServiceEventArgs> ServiceAdded;

    //Session-associated events 
    public event EventHandler<EntitySessionEventArgs> NewSession;
    public event EventHandler<EntitySessionEventArgs> SavingChanges;
    public event EventHandler<EntitySessionEventArgs> SavedChanges;
    public event EventHandler<EntityCommandEventArgs> ExecutedSelect;
    public event EventHandler<EntitySessionEventArgs> ExecutedQuery;
    public event EventHandler<EntitySessionEventArgs> ExecutedNonQuery;
    public event EventHandler<EntitySessionEventArgs> SaveChangesAborted;
    public event EventHandler<AppErrorEventArgs> Error;


    public EntityAppEvents(EntityApp app) {
      _app = app; 
    }

    internal void OnInitializing(EntityAppInitStep step) {
      if (Initializing != null)
        Initializing(_app, new AppInitEventArgs(_app, _app.ActivationLog, step)); 
    }

    internal void OnNewSession(EntitySession session) {
      if (NewSession != null)
        NewSession(session, new EntitySessionEventArgs(session));
    }
    internal void OnSavingChanges(EntitySession session) {
      if (SavingChanges != null)
        SavingChanges(session, new EntitySessionEventArgs(session));
    }
    internal void OnSavedChanges(EntitySession session) {
      if (SavedChanges != null)
        SavedChanges(session, new EntitySessionEventArgs(session));
    }
    internal void OnExecutedSelect(EntitySession session, EntityCommand command) {
      if (ExecutedSelect != null)
        ExecutedSelect(session, new EntityCommandEventArgs(session, command));
    }
    internal void OnExecutedQuery(EntitySession session, LinqCommand command) {
      if (ExecutedQuery != null)
        ExecutedQuery(session, new LinqCommandEventArgs(session, command));
    }
    internal void OnExecutedNonQuery(EntitySession session, LinqCommand command) {
      if(ExecutedNonQuery != null)
        ExecutedNonQuery(session, new LinqCommandEventArgs(session, command));
    }
    internal void OnSaveChangesAborted(EntitySession session) {
      if (SaveChangesAborted != null)
        SaveChangesAborted(session, new EntitySessionEventArgs(session));
    }
    internal void OnError(EntitySession session, Exception exception) {
      if (Error != null)
        Error(session, new AppErrorEventArgs(session, exception));
    }
    public void OnFlushRequested() {
      if(FlushRequested != null)
        FlushRequested(this, EventArgs.Empty);
    }
    internal void OnServiceAdded(EntityApp app, Type serviceType, object serviceInstance) {
      var evt = ServiceAdded;
      if (evt != null)
        evt(this, new ServiceEventArgs(app, serviceType, serviceInstance));
    }

  }//class


}//ns
