using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Xml;
using System.Diagnostics;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Linq;
using Vita.Data.Sql;

namespace Vita.Entities.Runtime {

  public sealed partial class EntityRecord  {
    public EntitySession Session;
    public EntityInfo EntityInfo;
    public WeakReference WeakSelfRef;
    public EntityMemberInfo StubParentMember;

    //If true, the record is registered in Session.RecordsLoaded set.
    public bool IsAttached;
    //Actual data 
    public object[] ValuesOriginal; //loaded from database
    public object[] ValuesModified;
    public object[] ValuesTransient; //EntityRef, EntityList, Computed values
    public bool SuppressAutoValues;    //if true, all auto-fields assignment is disabled

    public EntityCachingType SourceCacheType; //if coming from cache, indicates type of cache

    public List<ClientFault> ValidationFaults; //created on demand

    public EntityMemberMask MaskMembersRead;
    public EntityMemberMask MaskMembersChanged {
      get {
        if(_maskMembersChanged == null)
          _maskMembersChanged = new EntityMemberMask(this.EntityInfo.PersistentValuesCount);
        return _maskMembersChanged;
      }
    } EntityMemberMask _maskMembersChanged;

    //special secondary index for sorting records that are in non-trivial topological groups
    internal int SortSubIndex; 

    // temporary data existing during SaveChanges call - contains output parameters, identity param, etc.
    public EntityRecordDBCommandData DbCommandData; 

    // Can be used by special processes to store some temp data
    // used internally by batch process for records with identities
    public object CustomTag;

    public static readonly IList<EntityRecord> EmptyList = new EntityRecord[] { }; 

    #region Constructor and initialization

    //Creates a stub
    public EntityRecord(EntityKey primaryKey) : this(primaryKey.KeyInfo.Entity, EntityStatus.Stub) {
      //sanity check
      Util.Check(!primaryKey.IsNull(), "Primary key values are empty - cannot create a stub. Entity: {0}", EntityInfo.Name);
      _primaryKey = primaryKey;
      //copy primary key
      var keyInfo = primaryKey.KeyInfo;
      for (int i = 0; i < keyInfo.KeyMembersExpanded.Count; i++) {
        var member = keyInfo.KeyMembersExpanded[i].Member;
        var v = primaryKey.Values[i]; 
        ValuesOriginal[member.ValueIndex] = v;
      }
    }

    public EntityRecord(EntityInfo entityInfo, EntityStatus status) {
      EntityInfo = entityInfo;
      _status = status;
      WeakSelfRef = new WeakReference(this); 
      InitValuesStorage();
      MaskMembersRead = new EntityMemberMask(this.EntityInfo.PersistentValuesCount);
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
      _primaryKey = copyFrom.PrimaryKey;
      this._status = EntityStatus.Loaded;
      var valuesCount = EntityInfo.PersistentValuesCount;
      this.ValuesOriginal = new object[valuesCount];
      this.ValuesModified = new object[valuesCount];
      Array.Copy(copyFrom.ValuesOriginal, ValuesOriginal, valuesCount);
      this.ValuesTransient = new object[EntityInfo.TransientValuesCount];
      MaskMembersRead = new EntityMemberMask(valuesCount);
    }
    #endregion

    public event PropertyChangedEventHandler PropertyChanged;
    public string AliasId { get; set; }

    #region  PrimaryKey, Status, EntityInstance
    public EntityKey PrimaryKey {
      get {
        if(_primaryKey == null && EntityInfo.PrimaryKey != null)
          _primaryKey = new EntityKey(EntityInfo.PrimaryKey, this);
        return _primaryKey;
      }
      //internal set { _primaryKey = value; }
    }  EntityKey _primaryKey;

    public IEntityRecordContainer EntityInstance {
      get {
        if(_entityInstance == null)
          _entityInstance = EntityInfo.ClassInfo.CreateInstance(this);
        return _entityInstance;
      }
    }
    IEntityRecordContainer _entityInstance;


    public EntityStatus Status {
      get { return _status; }
      set {
        if(_status == value)
          return;
        var oldStatus = _status;
        _status = value;
        if(Session != null)
          Session.NotifyRecordStatusChanged(this, oldStatus);
      }
    } EntityStatus _status;

    //For Saved event, keeps old status after it had changed to know what was the update
    public EntityStatus StatusBeforeSave { get; private set; }

    #endregion

    #region Get/Set value
    public object GetValue(int index) {
      var member = EntityInfo.Members[index];
      return GetValue(member);
    }

    public void SetValue(int index, object value) {
      var member = EntityInfo.Members[index];
      SetValue(member, value); 
    }

    public object GetValue(string name) {
      var member = EntityInfo.GetMember(name, true);
      return GetValue(member);
    }

    public void SetValue(string name, object value) {
      var member = EntityInfo.GetMember(name, true);
      SetValue(member, value);
    }


    public object GetValue(EntityMemberInfo member) {
      return member.GetValueRef(this, member);
    }

    public void SetValue(EntityMemberInfo member, object value) {
      // if we are loading the record, just set the value and that's it.
      if(_status == EntityStatus.Loading || Session == null) {
        member.SetValueRef(this, member, value);
        return;
      }
      if(_status == EntityStatus.Stub)
        this.Reload();
      var oldStatus = _status;
      member.SetValueRef(this, member, value);
      //Fire modified event if necessary
      if(_status != oldStatus && _status == EntityStatus.Modified) {
        EntityInfo.Events.OnModified(this);
      }
      if(PropertyChanged != null) {
        OnPropertyChanged(member.MemberName);
        if(member.DependentMembers != null)
          for(int i = 0; i < member.DependentMembers.Length; i++) //for loop is more efficient
            OnPropertyChanged(member.DependentMembers[i].MemberName);
      }
    }

    public object GetRawValue(EntityMemberInfo member) {
      MaskMembersRead.Set(member);
      var valueIndex = member.ValueIndex;
      if(member.Kind == EntityMemberKind.Column)
        return ValuesModified[valueIndex] ?? ValuesOriginal[valueIndex];
      else
        return ValuesTransient[valueIndex];
    }

    public void SetValueDirect(EntityMemberInfo member, object value, bool setModified = false) {
      var valueIndex = member.ValueIndex;
      if(member.Kind == EntityMemberKind.Column) {
        if(_status == EntityStatus.Loading)
          ValuesOriginal[valueIndex] = value;
        else {
          MaskMembersChanged.Set(member);
          ValuesModified[valueIndex] = value;
        }
      } else
        ValuesTransient[valueIndex] = value ?? DBNull.Value;
      if(member.Flags.IsSet(EntityMemberFlags.PrimaryKey)) //identity setting from insert!
        this.PrimaryKey.CopyValues(this);
      if(setModified && this._status == EntityStatus.Loaded)
        Status = EntityStatus.Modified;
    }

    public bool HasValue(EntityMemberInfo member) {
      var value = GetRawValue(member);
      return value != null;
    }
    #endregion



    #region Save, Commit/cancel changes, Reload
    public void CommitChanges() {
      ValidationFaults = null; 
      this.StatusBeforeSave = _status; 
      if (Status == EntityStatus.Modified && EntityInfo.Flags.IsSet(EntityFlags.NoUpdate))
        Status = EntityStatus.Loaded;
      if (_status == EntityStatus.Loaded || _status == EntityStatus.Stub)
        return;
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
      this.DbCommandData = null; 
    }

    public void CancelChanges() {
      ValidationFaults = null;
      HashSet<EntityMemberInfo> modifiedMembers = null; 
      if (this.PropertyChanged != null)
        modifiedMembers = GetModifiedMembers(); 
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

    public void Reload() {
      Util.Check(Session != null, "Cannot reload record {0} - it is not attached to a session. ", EntityInfo);
      // Smart loading - for a stub, load not only this record, but all its siblings 
      // Note that it might not succeed to load all or even this one. The parent record(s) might be GC'd
      if (_status == EntityStatus.Stub && Session.SmartLoadEnabled && this.StubParentMember != null) {        
        if (this.Session.TryLoadEntityRefMemberForAllRecords(StubParentMember) && _status == EntityStatus.Loaded) //status after call to ReloadSiblings
          return; 
      }
      // regular way, load single record
      Session.SelectByPrimaryKey(this.EntityInfo, this.PrimaryKey.Values);
      ClearTransientValues();
    }

    #endregion

    #region Misc methods
    public int GetPropertySize(string name) {
      var member = EntityInfo.GetMember(name, true);
      return member.Size;
    }

    private HashSet<EntityMemberInfo> GetModifiedMembers() {
      var result = new HashSet<EntityMemberInfo>();
      if(_maskMembersChanged == null)
        return result; 
      foreach (var member in EntityInfo.Members)
        if (_maskMembersChanged.IsSet(member))
          result.Add(member);
      return result;
    }

    public bool Modified(EntityMemberInfo persistentMember) {
      if (persistentMember.Kind != EntityMemberKind.Column || _maskMembersChanged == null)
        return false;
      return _maskMembersChanged.IsSet(persistentMember);
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
        return $"(Error in ToString(): {ex.Message})";
      }
    }

    #endregion

    #region PropertyChanged handling

    private void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this.EntityInstance, new PropertyChangedEventArgs(propertyName));
    }
    #endregion

    #region ValidationErrors 

    public IList<ClientFault> GetValidationFaults() {
      return ValidationFaults; // ?? new List<ClientFault>(); 
    }
    public void AddValidationFault(ClientFault fault) {
      ValidationFaults = ValidationFaults ?? new List<ClientFault>();
      ValidationFaults.Add(fault);
      Session.Context.AddClientFault(fault);
    }


    public bool IsValid {
      get { return ValidationFaults == null || ValidationFaults.Count ==0; }
    }

    public void ClearValidation() {
      ValidationFaults = null; 
    }
    public string GetRecordRef() {
      if(!string.IsNullOrEmpty(AliasId))
        return AliasId;
      return EntityInfo.Name + "/" + PrimaryKey.ToString();
    }
    public ClientFault AddValidationError(string code, string message, object[] messageArgs, string targetProperty, object invalidValue = null) {
      var msg = Util.SafeFormat(message, messageArgs);
      var recId = GetRecordRef();
      var err = new ClientFault() { Code = code, Message = msg, Tag = targetProperty, Path = recId };
      if(invalidValue != null) {
        err.Parameters["InvalidValue"] = invalidValue.ToString();
      }
      AddValidationError(err);
      return err;
    }

    public void AddValidationError(ClientFault error) {
      if(ValidationFaults == null)
        ValidationFaults = new List<ClientFault>();
      ValidationFaults.Add(error);
      Session.Context.AddClientFault(error);
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


    public bool KeyIsNull(EntityKeyInfo key) {
      for(int i = 0; i < key.KeyMembersExpanded.Count; i++) {
        var valueIndex = key.KeyMembersExpanded[i].Member.ValueIndex;
        var v = ValuesModified[valueIndex] ?? ValuesOriginal[valueIndex];
        // Note: transient values in new record might be null
        if(v != DBNull.Value) return false;
      }
      return true;
    }
    public bool KeyIsLoaded(EntityKeyInfo key) {
      for(int i = 0; i < key.KeyMembersExpanded.Count; i++) {
        var valueIndex = key.KeyMembersExpanded[i].Member.ValueIndex;
        var v = ValuesModified[valueIndex] ?? ValuesOriginal[valueIndex];
        if(v == null) return false;
      }
      return true;
    }

    public bool KeyMatches(EntityKeyInfo keyInfo, object[] keyValues) {
      for(int i = 0; i < keyInfo.KeyMembersExpanded.Count; i++) {
        var member = keyInfo.KeyMembersExpanded[i].Member;
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
        if(member.Kind != EntityMemberKind.Column)
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
        case EntityMemberKind.Column:
        case EntityMemberKind.Transient:
        case EntityMemberKind.EntityRef:
          member.GetValueRef(this, member);
          break;
        case EntityMemberKind.EntityList:
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
          this.Session.RegisterForClearLists(this);
          break; 
      }
    }

    public IPropertyBoundList InitChildEntityList(EntityMemberInfo member) {
      var listInfo = member.ChildListInfo;
      var listType = (listInfo.RelationType == EntityRelationType.ManyToOne) ?
        typeof(PropertyBoundListManyToOne<>) : typeof(PropertyBoundListManyToMany<>);
      var genListType = listType.MakeGenericType(listInfo.TargetEntity.EntityType);
      var list = Activator.CreateInstance(genListType, this, member) as IPropertyBoundList;
      this.ValuesTransient[member.ValueIndex] = list; 
      return list;
    }

    public void RefreshIdentityReferences() {
      foreach(var refM in EntityInfo.RefMembers) {
        var targetEntInfo = refM.ReferenceInfo.ToKey.Entity;
        if(!targetEntInfo.Flags.IsSet(EntityFlags.HasIdentity))
          continue;
        var target = this.GetRawValue(refM);
        if(target == null || target == DBNull.Value)
          continue;
        var targetRec = EntityHelper.GetRecord(target);
        if(targetRec.Status != EntityStatus.New)
          continue;
        var idMember = targetRec.EntityInfo.IdentityMember;
        var idValue = targetRec.GetRawValue(idMember);
        var fkMember = refM.ReferenceInfo.FromKey.KeyMembersExpanded[0].Member;
        this.SetValue(fkMember, idValue); 
      }
    }

    public bool IsValueChanged(EntityMemberInfo member) {
      switch(member.Kind) {
        case EntityMemberKind.Column: return ValuesModified[member.ValueIndex] != null;
        default: return false; 
      }
    }
    #endregion


  }//class

}
