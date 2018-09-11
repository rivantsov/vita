using System;
using System.Collections; 
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Vita.Entities.Model;
//using System.Xml.Serialization;

namespace Vita.Entities.Runtime {

  // Note: we reimplement all IList<> methods (rather than simply inheriting from List<TEntity>) to catch all list modifications and 
  // fire INotifyCollectionChanged.CollectionChanged event. 
  /// <summary>Observable entity list class. Implements <c>INotifyCollectionChanged</c> interface. </summary>
  /// <typeparam name="TEntity">Entity type.</typeparam>
  /// <remarks>All entity lists returned by the system are instances of ObservableEntityList</remarks>
  internal class ObservableEntityList<TEntity> : IList<TEntity>, /* IList, */ INotifyCollectionChanged where TEntity: class {

    #region constructors
    public ObservableEntityList() { 
    }
    public ObservableEntityList(IList<IEntityRecordContainer> entities) {
      _entities = entities; 
    }
    #endregion

    public virtual bool Modified {get; set;}

    // Entities list might be set to null by list manager - this is an indicator that old list was stale, and must be reloaded.
    // Attached lists do not reload immediately when they detect that the list is stale (this happens on session.SaveChanges), to avoid
    // unnecessary database trips. Instead, we set the list to null, and it will be reloaded on the first access. 
    public IList<IEntityRecordContainer> Entities { 
      get {
        if (_entities == null) 
          LoadList(); //this should assign Entities and fire CollectionChanged event 
        return _entities;
      }
      set {
        _entities = value;
        if (CollectionChanged != null) 
          CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      } 
    } IList<IEntityRecordContainer> _entities;

    public bool IsLoaded { get {return _entities != null;}}

    public virtual void LoadList() {
    }

    public IList<EntityRecord> GetRecords() {
      if (!IsLoaded)
        LoadList();
      var result = new List<EntityRecord>();
      var entities = Entities;
      foreach (var ent in entities)
        result.Add(ent.Record);
      return result;
    }

    public virtual TEntity this[int index] {
      get { return (TEntity)Entities[index]; }
      set {
        var oldValue = Entities[index];
        Entities[index] = (IEntityRecordContainer) value;
        Modified = true;
        if(CollectionChanged != null)
          CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, oldValue));

      }
    }
    public virtual void Add(TEntity item) {
      var ent = item as IEntityRecordContainer;
      Util.Check(ent != null, "Attempt to add null to entity list property. Expected: {0}.", typeof(TEntity).Name);
      Entities.Add(ent);
      Modified = true;
      if(CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, this.Count));

    }

    public virtual void Clear() {
      Entities.Clear();
      Modified = true;
      if(CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
    public IEnumerator<TEntity> GetEnumerator() {
      foreach(var entity in Entities)
        yield return (TEntity)entity;
    }
    public virtual bool Contains(TEntity item) {
      return Entities.Contains((IEntityRecordContainer)item);
    }

    public virtual void CopyTo(TEntity[] array, int arrayIndex) {
      Entities.CopyTo(array.OfType<IEntityRecordContainer>().ToArray(), arrayIndex);
    }

    public int Count {
      get { return Entities.Count; }
    }
    public virtual void RemoveAt(int index) {
      var item = Entities[index];
      Entities.RemoveAt(index);
      Modified = true;
      if(CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }



    public bool IsReadOnly {
      get { return Entities.IsReadOnly; }
    }

    public virtual bool Remove(TEntity item) {
      var ent = (IEntityRecordContainer)item; 
      var index = Entities.IndexOf(ent);
      if(index < 0)
        return false; 
      RemoveAt(index);
      return true;
    }

    public virtual void Insert(int index, TEntity item) {
      Entities.Insert(index, (IEntityRecordContainer)item);
      Modified = true;
      if(CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return Entities.GetEnumerator();
    }

    public virtual int IndexOf(TEntity item) {
      return Entities.IndexOf((IEntityRecordContainer) item);
    }


    #region INotifyCollectionChanged Members

    public event NotifyCollectionChangedEventHandler CollectionChanged;

    #endregion

    public override string ToString() {
      if (_entities == null)
        return "(unloaded)";
      return "{Count=" + _entities.Count + "}";
    }
  }//class


}//namespace
