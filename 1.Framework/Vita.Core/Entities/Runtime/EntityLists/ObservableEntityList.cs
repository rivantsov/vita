using System;
using System.Collections; 
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
//using System.Xml.Serialization;

namespace Vita.Entities.Runtime {

  // Note: we reimplement all IList<> methods (rather than simply inheriting from List<TEntity>) to catch all list modifications and 
  // fire INotifyCollectionChanged.CollectionChanged event. 
  /// <summary>Observable entity list class. Implements <c>INotifyCollectionChanged</c> interface. </summary>
  /// <typeparam name="TEntity">Entity type.</typeparam>
  /// <remarks>All entity lists returned by the system are instances of ObservableEntityList</remarks>
  public class ObservableEntityList<TEntity> : IList<TEntity>, IList, INotifyCollectionChanged  {

    #region constructors
    public ObservableEntityList() { 
    }
    public ObservableEntityList(IList entities) {
      _entities = entities; 
    }
    #endregion

    public virtual bool Modified {get; set;}

    // Entities list might be set to null by list manager - this is an indicator that old list was stale, and must be reloaded.
    // Attached lists do not reload immediately when they detect that the list is stale (this happens on session.SaveChanges), to avoid
    // unnecessary database trips. Instead, we set the list to null, and it will be reloaded on the first access. 
    public IList Entities { 
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
    } IList _entities;

    public bool IsLoaded { get {return _entities != null;}}

    public virtual void LoadList() {
    }

    public IList<EntityRecord> GetRecords() {
      if (!IsLoaded)
        LoadList();
      var result = new List<EntityRecord>();
      var entities = Entities;
      foreach (var ent in entities)
        result.Add(EntityHelper.GetRecord(ent));
      return result;
    }


    #region IList implementation
    public virtual int IndexOf(TEntity item) { 
      return Entities.IndexOf(item); 
    }

    public virtual void Insert(int index, TEntity item) {
      Entities.Insert(index, item);
      Modified = true;
      if (CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    public virtual void RemoveAt(int index) {
      var item = Entities[index];
      Entities.RemoveAt(index);
      Modified = true;
      if (CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }

    public virtual TEntity this[int index] {
        get { return (TEntity) Entities[index]; }
        set {
          var oldValue = Entities[index];
          Entities[index] = value;
          Modified = true;
          if (CollectionChanged != null)
            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, oldValue));

        }
    }

    public virtual void Add(TEntity item) {
      Vita.Common.Util.Check(item != null, "Attempt to add null to entity list property. Expected: {0}.", typeof(TEntity).Name);
      Entities.Add(item);
      Modified = true;
      if (CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, this.Count));

    }

    public virtual void Clear() {
      Entities.Clear();
      Modified = true;
      if (CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public virtual bool Contains(TEntity item) {
      return Entities.Contains(item); 
    }

    public virtual void CopyTo(TEntity[] array, int arrayIndex) {
      Entities.CopyTo(array, arrayIndex); 
    }

    public int Count {
        get { return Entities.Count; }
    }

    public bool IsReadOnly {
        get { return Entities.IsReadOnly; }
    }

    public virtual bool Remove(TEntity item) {
      var result = Entities.Contains(item);
      if (result) 
        this.RemoveItem(item);
      return result; 
    }

    public IEnumerator<TEntity> GetEnumerator() {
      foreach (var entity in Entities)
        yield return (TEntity) entity; 
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return Entities.GetEnumerator();
    }

    public virtual int Add(object value) {
      var result = Entities.Add (value);
      Modified = true; 
      if (CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, this.Count));
      return result; 
    }

    public virtual bool Contains(object value) {
      return Entities.Contains(value);
    }

    public virtual int IndexOf(object value) {
      return Entities.IndexOf(value);
    }

    public virtual void Insert(int index, object value) {
      Entities.Insert(index, value);
      Modified = true;
      if (CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, index, value));
    }

    public virtual bool IsFixedSize {
      get { return Entities.IsFixedSize; }
    }

    public virtual void Remove(object value) {
      this.RemoveItem(value); 
    }

    object IList.this[int index] {
      get { return Entities[index]; }
      set { Entities[index] = value; }
    }

    public virtual void CopyTo(Array array, int index) {
      Entities.CopyTo(array, index); 
    }

    public bool IsSynchronized {
      get { return Entities.IsSynchronized; }
    }

    public virtual object SyncRoot {
      get { return Entities.SyncRoot; }
    }

    protected virtual void RemoveItem(object entity) {
      var index = IndexOf(entity);
      Entities.RemoveAt(index);
      Modified = true;
      if (CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, entity, index));
    }

    #endregion

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
