using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Xml;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities.Authorization;
using Vita.Entities.Caching;
using Vita.Entities.Model;
using Vita.Entities.Authorization.Runtime;

namespace Vita.Entities.Runtime {

  public enum EntityStatus {
    Stub, //an empty data record with only PK fields initialized
    Loading,  //in the process of loading from a data store
    Loaded,
    Modified,
    New,
    Deleting, //marked for deletion
    Fantom, //just deleted in database; or: created as new but then marked for deletion; so no action in database
  }

  public partial class EntityRecord {
    public EntityInfo EntityInfo;
    public EntitySession Session;
    //If true, the record is registered in Session.RecordsLoaded set.
    public bool IsAttached;
    //Actual data 
    public object[] ValuesOriginal; //loaded from database
    public object[] ValuesModified;
    public object[] ValuesTransient; //EntityRef, EntityList, Computed values
    public bool SuppressAutoValues;    //if true, all auto-fields assignment is disabled

    public CacheType SourceCacheType; //if coming from cache, indicates type of cache

    public List<ClientFault> ValidationErrors; //created on demand

    //Authorization info
    public UserRecordPermission UserPermissions; 
    public UserRecordPermission ByRefUserPermissions; //delegated permissions

    //Counts # of times record had been submitted to physical database. Should be at least 1 after ApplyUpdates
    public int SubmitCount;


    // Index used in updates sorting; derived from EntityInfo.TopologicalIndex
    public int SortIndex;
    public int SortSubIndex; //special secondary index for sorting records that are in non-trivial topological groups

    // Can be used by special processes to store some temp data
    // used internally by batch process for records with identities
    internal object CustomTag; 

    #region Constructor and initialization
    // Used by deserialization, when we serialize/deserialize records, not entities.
    public EntityRecord() {
      _status = EntityStatus.Loading;
      // do not call InitValuesStorage yet - EntityInfo is not known yet
    }

    //Creates a stub
    public EntityRecord(EntityKey primaryKey) : this(primaryKey.KeyInfo.Entity, EntityStatus.Stub) {
      //sanity check
      Util.Check(!primaryKey.IsNull(), "Primary key values are empty - cannot create a stub. Entity: {0}", EntityInfo.Name);
      _primaryKey = primaryKey;
      //copy primary key
      var keyInfo = primaryKey.KeyInfo;
      for (int i = 0; i < keyInfo.ExpandedKeyMembers.Count; i++) {
        var member = keyInfo.ExpandedKeyMembers[i].Member;
        var v = primaryKey.Values[i]; 
        ValuesOriginal[member.ValueIndex] = v;
      }
    }

    public EntityRecord(EntityInfo entityInfo, EntityStatus status) {
      _status = status;
      EntityInfo = entityInfo;
      InitValuesStorage();
    }

    internal void InitValuesStorage() {
      ValuesOriginal = new object[EntityInfo.PersistentValuesCount];
      ValuesModified = new object[EntityInfo.PersistentValuesCount];
      ValuesTransient = new object[EntityInfo.TransientValuesCount];
      if (_status == EntityStatus.New) {
        Array.Copy(EntityInfo.InitialColumnValues, ValuesModified, ValuesModified.Length);
      }
    }

    //Creates a clone from a copy
    public EntityRecord(EntityRecord copyFrom) {
      this.EntityInfo = copyFrom.EntityInfo;
      this.PrimaryKey = copyFrom.PrimaryKey;
      this._status = EntityStatus.Loaded;
      var valuesCount = EntityInfo.PersistentValuesCount;
      this.ValuesOriginal = new object[valuesCount];
      this.ValuesModified = new object[valuesCount];
      Array.Copy(copyFrom.ValuesOriginal, ValuesOriginal, valuesCount);
      this.ValuesTransient = new object[EntityInfo.TransientValuesCount];
    }
    #endregion

    #region Public properties: PrimaryKey, Status, EntityInstance
    public EntityKey PrimaryKey {
      get {
        if(_primaryKey == null && EntityInfo.PrimaryKey != null)
          _primaryKey = new EntityKey(EntityInfo.PrimaryKey, this);
        return _primaryKey; 
      }
      internal set { _primaryKey = value; }
    } EntityKey _primaryKey; 

    public EntityStatus Status {
      get { return _status; }
      set {
        if (_status == value)
          return;
        var oldStatus = _status;
        _status = value;
        if (Session != null)
          Session.NotifyRecordStatusChanged(this, oldStatus);
      }
    } EntityStatus _status;

    //For Saved event, keeps old status after it had changed to know what was the update
    public EntityStatus StatusBeforeSave {get; private set;}

    public bool AccessCheckEnabled {
      get { return _status != EntityStatus.Loading && _status != EntityStatus.New; }
    }

    public EntityBase EntityInstance {
      get {
        if (_entityInstance == null)
          _entityInstance = EntityInfo.ClassInfo.CreateInstance(this);
        return _entityInstance; 
      }
    }  EntityBase _entityInstance;

    #endregion

    #region Save, Commit/cancel changes
    public void CommitChanges() {
      ValidationErrors = null; 
      this.StatusBeforeSave = _status; 
      if (Status == EntityStatus.Modified && EntityInfo.Flags.IsSet(EntityFlags.NoUpdate))
        Status = EntityStatus.Loaded;
      if (_status == EntityStatus.Loaded || _status == EntityStatus.Stub)
        return;
      const string msg =
        "Entity {0} had not been submitted to any persistent store on update. Check that entity area {1} is mapped to a datastore.";
      Util.Check(SubmitCount > 0, msg, this.EntityInfo.FullName, this.EntityInfo.Area.Name);
      SubmitCount = 0;
      if (Status == EntityStatus.Deleting) {
        Status = EntityStatus.Fantom;
        return;
      }
      //Copy values from ModifiedValues to OriginalValues
      for (int i = 0; i < ValuesOriginal.Length; i++) {
        var newValue = ValuesModified[i];
        if (newValue == null) continue; //was not assigned
        ValuesOriginal[i] = newValue;
        ValuesModified[i] = null;
      }
      Status = EntityStatus.Loaded;
    }

    public void CancelChanges() {
      ValidationErrors = null;
      HashSet<EntityMemberInfo> modifiedMembers = null; 
      if (this.PropertyChanged != null)
        modifiedMembers = GetModifiedMembers(); 
      SubmitCount = 0;
      switch (Status) {
        case EntityStatus.Loaded: return; 
        case EntityStatus.New: Status = EntityStatus.Fantom; break;
        case EntityStatus.Modified: Status = EntityStatus.Loaded; break;
        default: Status = EntityStatus.Loaded; break; 
      }
      // Save modified values, we will need it for invoking PropertyChanged
      var savedValues = ValuesModified;
      //Create new empty array for modified values - this wipes out changes
      ValuesModified = new object[EntityInfo.PersistentValuesCount];
      //wipe out transients - in case entity refs were assigned
      ValuesTransient = new object[EntityInfo.TransientValuesCount];
      //If somebody is listening to PropertyChanged, fire it for every property that was assigned
      if (this.PropertyChanged != null) 
        foreach (var member in modifiedMembers) 
            OnPropertyChanged(member.MemberName);
    }

    #endregion

    #region GetValue, SetValue methods

    public object this[string memberName] {
      get { return GetValue(memberName); }
      set { SetValue(memberName, value); }
    }


    public object GetValue(int memberIndex) {
      var member = EntityInfo.Members[memberIndex];
      return GetValue(member);
    }

    public object GetValue(string memberName) {
      var member = EntityInfo.GetMember(memberName, true);
      return GetValue(member);
    }
    public object GetValue(EntityMemberInfo member) {
      //If there's session, go thru session. Authorization-enabled session will use this method to check permissions
      if (Session == null)
        return member.GetValueRef(this, member);
      else
        return Session.RecordGetMemberValue(this, member);
    }


    public void SetValue(int memberIndex, object value) {
      var member = EntityInfo.Members[memberIndex];
      SetValue(member, value);
    }
    
    public void SetValue(string memberName, object value) {
      var member = EntityInfo.GetMember(memberName, true);
      SetValue(member, value);
    }

    public void SetValue(EntityMemberInfo member, object value) {
      // if we are loading the record, just set the value and that's it.
      if (_status == EntityStatus.Loading || Session == null) {
        member.SetValueRef(this, member, value);
        return; 
      }
      if (_status == EntityStatus.Stub)
        this.Reload();
      var oldStatus = _status;
      // Go thru session. Authorization-enabled session will use this method to check permissions
      Session.RecordSetMemberValue(this, member, value);
      //Fire modified event if necessary
      if (_status != oldStatus && _status == EntityStatus.Modified) {
        EntityInfo.Events.OnModified(this);
        Session.Context.App.EntityEvents.OnModified(this);
      }
      if (PropertyChanged != null) {
        OnPropertyChanged(member.MemberName);
        if (member.DependentMembers != null)
          for (int i=0; i< member.DependentMembers.Length; i++) //for loop is more efficient
            OnPropertyChanged(member.DependentMembers[i].MemberName);
      }
    }

    public object GetValueDirect(EntityMemberInfo member) {
      var valueIndex = member.ValueIndex; 
      if (member.Kind == MemberKind.Column)
        return ValuesModified[valueIndex] ?? ValuesOriginal[valueIndex];
      else 
        return ValuesTransient[valueIndex];
    }

    public void SetValueDirect(EntityMemberInfo member, object value, bool setModified = false) {
      var valueIndex = member.ValueIndex;
      if (member.Kind == MemberKind.Column) {
        if (_status == EntityStatus.Loading)
          ValuesOriginal[valueIndex] = value;
        else 
          ValuesModified[valueIndex] = value;
      } else 
        ValuesTransient[valueIndex] = value ?? DBNull.Value;
      if (member.Flags.IsSet(EntityMemberFlags.PrimaryKey)) //identity setting from insert!
        this.PrimaryKey.CopyValues(this);
      if (setModified && this._status == EntityStatus.Loaded)
        Status = EntityStatus.Modified; 
    }

    public bool HasValue(EntityMemberInfo member) {
      var value = GetValueDirect(member); 
      return value != null; 
    }

    private HashSet<EntityMemberInfo> GetModifiedMembers() {
      var result = new HashSet<EntityMemberInfo>();
      foreach (var member in EntityInfo.Members)
        if (Modified(member))
          result.Add(member);
      return result;
    }

    public bool Modified(EntityMemberInfo persistentMember) {
      if (persistentMember.Kind != MemberKind.Column)
        return false; 
      return ValuesModified[persistentMember.ValueIndex] != null;
    }

    public IPropertyBoundList InitChildEntityList(EntityMemberInfo member) {
      var listInfo = member.ChildListInfo;
      Util.Check(Session != null, "Failed to create child entity list for member {0}. Record is not attached to session.", member);
      var listType = (listInfo.RelationType == EntityRelationType.ManyToOne) ?
        typeof(PropertyBoundListManyToOne<>) : typeof(PropertyBoundListManyToMany<>);
      var genListType = listType.MakeGenericType(listInfo.TargetEntity.EntityType);
      var list = Activator.CreateInstance(genListType, this, member) as IPropertyBoundList;
      this.ValuesTransient[member.ValueIndex] = list;
      return list;
    }


    #endregion

    #region Overrides: ToString, GetHashCode()
    /// <summary>Returns a display string for an entity. The display string is produced by a custom method specified in [Display] attribute.</summary>
    public override string ToString() {
      try {
        if(EntityInfo.DisplayMethod != null) {
          return EntityInfo.DisplayMethod(this);
        } else
          return PrimaryKey + "/" + _status; 
      } catch(Exception ex) {
        return "(Error in ToString(): " + ex.Message + ")";
      }
    }

    public override int GetHashCode() {
      if (_primaryKey == null)
        return 0; 
      return _primaryKey.GetHashCode();
    }
    #endregion

    #region PropertyChanged handling
    //PropertyChanged event handlers are passed from Entity to this event
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName) {
      if (PropertyChanged != null)
        PropertyChanged(this.EntityInstance, new PropertyChangedEventArgs(propertyName));
    }
    #endregion

    #region ValidationErrors 
    public bool IsValid {
      get { return ValidationErrors == null || ValidationErrors.Count ==0; }
    }
    public void ClearValidation() {
      ValidationErrors = null; 
    }
    public void AddValidationError(ClientFault error) {
      if(ValidationErrors == null)
        ValidationErrors = new List<ClientFault>();
      ValidationErrors.Add(error);
      Session.Context.ClientFaults.Add(error);
    }
    public ClientFault AddValidationError(string code, string message, object[] messageArgs, string targetProperty, object invalidValue = null) {
      var msg = StringHelper.SafeFormat(message, messageArgs);
      var recPath = EntityInfo.Name + "/" + PrimaryKey.ToString();
      var err = new ClientFault() { Code = code, Message = msg, Tag = targetProperty, Path = recPath};
      if (invalidValue != null) {
        err.Parameters["InvalidValue"] = invalidValue.ToString();
      }
      AddValidationError(err);
      return err;
    }
    #endregion


    #region Miscellaneous helper methods
    public bool NeedsSave() {
      switch(_status) {
        case EntityStatus.New:
        case EntityStatus.Modified:
        case EntityStatus.Deleting:
          return true;
        default:
          return false;
      }
    }//method


    public void Reload(bool disableCache = false) {
      Util.Check(Session != null, "Cannot reload record {0} - it is not attached to a session. ", EntityInfo);
      // Executing SelectByPrimaryKey automatically refreshes the values in the record
      // In some situations we need to elevate read, 
      // Ex: record permissions are granted thru reference on other entity; user has no any permissions for entity type, 
      // so SecureSession.ExecuteSelect would permissions for entity type and throw AccessDenied
        //if (cacheEnabled) Session.EnableCache(false); 
      using (Session.ElevateRead()) {
        if (disableCache)
          using(Session.WithNoCache())
            Session.ExecuteSelect(EntityInfo.CrudCommands.SelectByPrimaryKey, PrimaryKey.Values);
        else 
          Session.ExecuteSelect(EntityInfo.CrudCommands.SelectByPrimaryKey, PrimaryKey.Values);

      }
    }

    public bool KeyIsNull(EntityKeyInfo key) {
      for(int i = 0; i < key.ExpandedKeyMembers.Count; i++) {
        var valueIndex = key.ExpandedKeyMembers[i].Member.ValueIndex;
        var v = ValuesModified[valueIndex] ?? ValuesOriginal[valueIndex];
        // Note: transient values in new record might be null
        if(v != DBNull.Value) return false;
      }
      return true;
    }
    public bool KeyIsLoaded(EntityKeyInfo key) {
      for(int i = 0; i < key.ExpandedKeyMembers.Count; i++) {
        var valueIndex = key.ExpandedKeyMembers[i].Member.ValueIndex;
        var v = ValuesModified[valueIndex] ?? ValuesOriginal[valueIndex];
        if(v == null) return false;
      }
      return true;
    }

    public bool KeyMatches(EntityKeyInfo keyInfo, object[] keyValues) {
      for(int i = 0; i < keyInfo.ExpandedKeyMembers.Count; i++) {
        var member = keyInfo.ExpandedKeyMembers[i].Member;
        var thisValue = GetValue(member);
        var otherValue = keyValues[i];
        var eq = member.AreValuesEqual(thisValue, otherValue);
        if(!eq)
          return false;
      }
      return true;
    }

    public string GetNaturalKey() {
      //TODO: implement Natural key
      return "(Natural key not implemented)";
    }

    public void CopyOriginalValues(EntityRecord fromRecord) {
      Array.Copy(fromRecord.ValuesOriginal, this.ValuesOriginal, this.ValuesOriginal.Length);
      if(this.Status == EntityStatus.Stub)
        Status = EntityStatus.Loaded;
    }
    public void ClearTransientValues() {
      for(int i = 0; i < ValuesTransient.Length; i++)
        ValuesTransient[i] = null;
    }
    public void ClearEntityRefValues() {
      var refMembers = EntityInfo.RefMembers;
      if(refMembers.Count > 0)
        for(int i = 0; i < refMembers.Count; i++)
          ValuesTransient[refMembers[i].ValueIndex] = null;
    }

    public void MarkForDelete() {
      Status = (Status == EntityStatus.New) ? EntityStatus.Fantom : EntityStatus.Deleting;
    }

    // Ensures that transient (entity ref and entity lists) member values are loaded. Used by entity cache to eager load properties
    // of cached entities
    public void EnsureNonColumnMembers() {
      foreach(var member in EntityInfo.Members)
        if(member.Kind != MemberKind.Column)
          EnsureLoaded(member);
    }

    public void EnsureLoaded(IEnumerable<EntityMemberInfo> members) {
      if(members == null)
        return;
      foreach(var member in members)
        EnsureLoaded(member);
    }//method

    public void EnsureLoaded(EntityMemberInfo member) {
      switch(member.Kind) {
        case MemberKind.Column:
        case MemberKind.Transient:
        case MemberKind.EntityRef:
          member.GetValueRef(this, member);
          break;
        case MemberKind.EntityList:
          // for lists, touch Count property to make sure it is loaded/refreshed
          // If the list had been accessed/loaded before, but then marked as stale then internal list was cleared - let's force reload
          var list = member.GetValueRef(this, member) as IList;
          var count = list.Count;
          break;
      }//switch
    }

    public void MarkForClearLists() {
      switch(this._status) {
        case EntityStatus.Loaded: case EntityStatus.Modified: case EntityStatus.Stub:
          this.Session.RecordsToClearLists.Add(this);
          break; 
      }
    }
    #endregion
    
    
  }//class

}
