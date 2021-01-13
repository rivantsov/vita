using System;
using System.Collections.Generic;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Model.Construction;

namespace Vita.Entities {

  public enum EntityAppInitStep {
    Initializing,
    EntityModelConstructed,
    Initialized,
  }


  public class AppInitEventArgs : EventArgs {
    public readonly EntityApp App;
    public readonly EntityAppInitStep Step;

    public AppInitEventArgs(EntityApp app, EntityAppInitStep step) {
      App = app;
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

  public class AppErrorEventArgs : EventArgs {
    public readonly OperationContext Context;
    public readonly Exception Exception;
    public AppErrorEventArgs(OperationContext context, Exception exception) {
      Context = context;
      Exception = exception;
    }
  }//class

  public class ConnectedEventArgs : EventArgs {
    public string DataSourceName;
    public ConnectedEventArgs(string dataSourceName) {
      DataSourceName = dataSourceName;
    }
  }

  public class EntityCommandEventArgs : EventArgs {
    public readonly IEntitySession Session;
    public readonly object Command;
    public EntityCommandEventArgs(IEntitySession session, object command) {
      Session = session;
      Command = command;
    }
  }//class

  public class EntityModelConstructEventArgs : EventArgs {
    public EntityModelState ModelState { get; internal set; }
    public EntityModel Model { get; internal set; }
    public ILog Log { get; internal set; }
  }



  //Events for the entire entity store
  public class EntityAppEvents {
    EntityApp _app; 

    public event EventHandler<AppInitEventArgs> Initializing; //fired multiple times
    public event EventHandler<EntityModelConstructEventArgs> ModelConstructing;
    public event EventHandler FlushRequested;
    public event EventHandler<ServiceEventArgs> ServiceAdded;
    public event EventHandler<ConnectedEventArgs> Connected;

    //Session-associated events 
    public event EventHandler<EntitySessionEventArgs> NewSession;
    public event EventHandler<EntitySessionEventArgs> SavingChanges;
    public event EventHandler<EntitySessionEventArgs> SavedChanges;
    public event EventHandler<EntitySessionEventArgs> SaveChangesAborted;
    public event EventHandler<EntityCommandEventArgs> ExecutedSelect;
    public event EventHandler<EntityCommandEventArgs> ExecutedNonQuery;
    public event EventHandler<AppErrorEventArgs> Error;
    public event EventHandler Shutdown;

    public EntityAppEvents(EntityApp app) {
      _app = app;
    }


    internal void OnInitializing(EntityAppInitStep step) {
      Initializing?.Invoke(_app, new AppInitEventArgs(_app, step)); 
    }

    internal void OnModelConstructing(EntityModelBuilder builder) {
      ModelConstructing?.Invoke(builder, new EntityModelConstructEventArgs() { Model = builder.Model, ModelState = builder.Model.ModelState, Log = builder.Log });
    }

    internal void OnShutdown() {
      Shutdown?.Invoke(_app, EventArgs.Empty);
    }

    internal void OnConnected(string dataSourceName) {
      Connected?.Invoke(_app, new ConnectedEventArgs(dataSourceName));
    }

    internal void OnNewSession(IEntitySession session) {
      NewSession?.Invoke(session, new EntitySessionEventArgs(session));
    }
    internal void OnSavingChanges(IEntitySession session) {
      SavingChanges?.Invoke(session, new EntitySessionEventArgs(session));
    }
    internal void OnSavedChanges(IEntitySession session) {
        SavedChanges?.Invoke(session, new EntitySessionEventArgs(session));
    }
    internal void OnSaveChangesAborted(IEntitySession session) {
      SaveChangesAborted?.Invoke(session, new EntitySessionEventArgs(session));
    }
    internal void OnExecutedSelect(IEntitySession session, object command) {
      ExecutedSelect?.Invoke(session, new EntityCommandEventArgs(session, command));
    }
    internal void OnExecutedNonQuery(IEntitySession session, object command) {
      ExecutedNonQuery?.Invoke(session, new EntityCommandEventArgs(session, command));
    }
    public void OnError(OperationContext context, Exception exception) {
      Error?.Invoke(context, new AppErrorEventArgs(context, exception));
    }
    public void OnFlushRequested() {
      FlushRequested?.Invoke(this, EventArgs.Empty);
    }
    internal void OnServiceAdded(EntityApp app, Type serviceType, object serviceInstance) {
        ServiceAdded?.Invoke(this, new ServiceEventArgs(app, serviceType, serviceInstance));
    }

  }//class


}//ns
