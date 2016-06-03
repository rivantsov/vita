using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Globalization;
using System.Linq.Expressions;
using System.Data;

using Vita.Common;
using Vita.Common.Graphs;
using Vita.Entities.Runtime;
using Vita.Entities.Model.Construction;
using Vita.Entities.Caching;
using Vita.Entities.Linq;

namespace Vita.Entities.Model {

  // Holds CRUD commands for a table
  public class EntityCrudCommands {
    public EntityCommand SelectAll;
    public EntityCommand SelectAllPaged;
    public EntityCommand SelectByPrimaryKey;
    public EntityCommand SelectByPrimaryKeyArray;
    public EntityCommand Insert;
    public EntityCommand Delete;
    public EntityCommand Update;
  }//class


  public delegate string RecordDisplayMethod(EntityRecord record);

  [Flags]
  public enum EntityFlags {
    None = 0,
    //============ Flags set by attributes ==========================
    //

    // Marks entities that are created on the fly when update is performed (ex: log records), and should be eliminated after transaction aborts or commits  
    DiscardOnAbourt = 1 << 2,

    // Entities are cached
    Cached = 1 << 3,

    // Do not do authorization checks
    BypassAuthorization = 1 << 4,

    // =================== Flags computed at runtime (setup time) ========================
    //Self-referencing entities are not handled by SCC algorithm, so we track them with this flag
    SelfReferencing = 1 << 8, 
    // true if referenced by other entities
    IsReferenced = 1 << 9, 
    //if true, you can only select/insert/delete. Typical case is link table, with PK consisting of entity key references
    NoUpdate = 1 << 10,
    // Set by Scc algorithm, identifies that entity's SCC group has more than one entity
    TopologicalGroupNonTrivial = 1 << 11,
    // Some 'strange' tables (like Production.Document in AdventureWorks database) have strange PKs (heirarchyid type). 
    // LINQ pukes on attempt to map such a table
    LinqDisabled = 1 << 14,

    // True if entity/table has identity column
    HasIdentity = 1 << 16,
    // set if there's a RowVersion member
    HasRowVersion = 1 << 17,
    HasClusteredIndex = 1 << 18,

    // Do not track in TransactionLog
    DoNotTrack = 1 << 19,

  }

  public delegate EntityBase EntityCreatorMethod(EntityRecord record);

  //contains information about generated class
  public class EntityClassInfo {
    public Type Type;
    public EntityCreatorMethod CreateInstance;
  }

  // We use entity Model objects as keys in DbModel's tables(dictionaries) of all db objects - so they all derive from HashedObject.
  // For example, to find a table for an entity (find DbTableInfo for an EntityInfo). 
  // Note that there may be multiple DbModels for a single entity Model, so we may have a different DB object in each DbModel for an entity object.

  public class EntityInfo : HashedObject {
    public readonly EntityModule Module;
    public EntityArea Area;
    public string Name;
    public string FullName { get; private set; }
    public string TableName; // explicitly set table name in Entity attribute
    public EntityKind Kind;
    public ViewDefinition ViewDefinition;

    public string Description;

    public readonly Type EntityType;
    public readonly HashSet<Type> ReplacesTypes = new HashSet<Type>(); //in case of entity replacement, the types that this type is replacing
    public EntityInfo ReplacedBy; 
    public readonly List<EntityMemberInfo> Members = new List<EntityMemberInfo>();
    public readonly List<EntityKeyInfo> Keys = new List<EntityKeyInfo>(); 
    public EntityKeyInfo PrimaryKey;
    public Dictionary<string, EntityMemberInfo> MembersByName = new Dictionary<string, EntityMemberInfo>(StringComparer.InvariantCultureIgnoreCase);
    public IList<Type> CompanionTypes = new List<Type>(); // Companion classes or interfaces - derived from Entity, 
                                                          // and specifying additional attributes
    public List<Attribute> Attributes = new List<Attribute>(); //attributes on entity itself and on companion types
    public List<EntityMemberInfo> RefMembers = new List<EntityMemberInfo>();
    public List<EntityMemberInfo> IncomingReferences = new List<EntityMemberInfo>(); // members that reference this entity
    public EntityMemberInfo OwnerMember; //A reference member marked with Owner attribute

    public int PersistentValuesCount; // Size of ValuesOriginal/ValuesModified arrays in EntityRecord
    public int TransientValuesCount;    // Size of ValuesTransient array in EntityRecord

    public IList<PropertyGroup> PropertyGroups = new List<PropertyGroup>();
    public RecordDisplayMethod DisplayMethod;
    public EntityMemberInfo VersioningMember; // member holding LastModified-transaction-id, if entity is tracked

    public EntityFlags Flags;
    public CacheType CacheType;

    public readonly EntityEvents Events = new EntityEvents();
    public readonly EntitySaveEvents SaveEvents = new EntitySaveEvents();
    internal object[] InitialColumnValues;


    //Topological sorting index, assigned by SCC (Strongly-Connected Components) algorithm
    public int TopologicalIndex;
    internal Vertex SccVertex; //used in SCC algorithm

    //Entity classes information
    public EntityClassInfo ClassInfo; 
    //Paging mode
    public PagingMode PagingMode;

    public IList<EntityKeyMemberInfo> DefaultOrderBy;

    public string[] OldNames; //From OldNames attribute 

    //CRUD
    public EntityCrudCommands CrudCommands {get; internal set;}

    public EntityInfo(EntityModule module, Type entityType, EntityKind kind = EntityKind.Table) {
      Module = module;
      EntityType = entityType;
      Kind = kind; 
      Name = entityType.Name;
      //Check for generic types - happens in modules with generic entities (interfaces), provided for customization
      if(Name.Contains('`'))
        Name = Name.Substring(0, Name.IndexOf('`'));
      if(EntityType.IsInterface && Name.Length > 1 && Name.StartsWith("I"))
        Name = Name.Substring(1);
      //check if entity was moved
      if (!module.App.MovedEntities.TryGetValue(entityType, out this.Area))
        Area = Module.Area; 
      FullName = Area.Name + "." + Name;
    }


    public override string ToString() {
      return EntityType.ToString();
    }
    public EntityMemberInfo GetMember(string name, bool throwIfNotFound = false) {
      if(name != null)
        name = name.Trim().ToLowerInvariant(); 
      EntityMemberInfo member;
      if(MembersByName.TryGetValue(name, out member)) 
        return member;
      if (throwIfNotFound)
        throw new Exception(StringHelper.SafeFormat("Member {0} not found in entity {1}.", name, EntityType.Name));
      return null; 
    }

    public EntityMemberInfo FindMemberOrColumn(string name) {
      name = name.Trim();
      foreach (var member in Members)
        if (member.MemberName == name || string.Equals(member.ColumnName, name, StringComparison.InvariantCultureIgnoreCase))
          return member; 
      return null;
    }

    public EntityMemberInfo GetMember<TEntity>(Expression<Func<TEntity, object>> propertySelector) {
      var propName = ReflectionHelper.GetSelectedProperty(propertySelector);
      return GetMember(propName, throwIfNotFound: true);
    }

    public T GetAttribute<T>() where T : Attribute {
      return (T)Attributes.Find(a => a is T);
    }
    public bool HasAttribute<AttributeType>() where AttributeType : Attribute {
      var attr = GetAttribute<AttributeType>();
      return attr != null;
    }

    public void AddMember(EntityMemberInfo member) {
      try {
        member.Index = this.Members.Count;
        Members.Add(member);
        MembersByName.Add(member.MemberName, member);
      } catch(Exception ex) {
        ex.AddValue("MemberName", member.MemberName);
        throw; 
      }
    }

    public PropertyGroup GetPropertyGroup(string name, bool create = false) {
      if (name.StartsWith("@"))
        name = name.Substring(1); //cutoff @
      var g = PropertyGroups.FirstOrDefault(gr => gr.Name == name);
      if (g == null && create) {
        g = new PropertyGroup(name);
        PropertyGroups.Add(g);
      }
      return g; 
    }
  }//EntityInfo

  //Property groups are defined in PropertyGroup attribute
  public class PropertyGroup {
    public string Name;
    public List<EntityMemberInfo> Members = new List<EntityMemberInfo>();
    public PropertyGroup(string name) {
      Name = name; 
    }
  }

  [Flags]
  public enum EntityMemberFlags {
    None = 0, 

    Nullable = 0x01,
    AutoValue = 1 << 1,
    NoDbInsert = 1 << 2, // do not include in insert statement
    NoDbUpdate = 1 << 3, //If set, the field is not included in CRUD update statement

    PrimaryKey = 1 << 4,
    ForeignKey = 1 << 5,
    ClusteredIndex = 1 << 6,
    Identity = 1 << 7,

    IsOwner = 1 << 8,
    ReplaceDefaultWithNull = 1 << 9, //for nullable value types (NOTE: not types like double?; but declared "double" and marked with Nullable attribute
    Computed = 1 << 10, //value is automatically computed
    CascadeDelete = 1 << 11,
    Secret = 1 << 12,  //Write only, no selects
    Utc = 1 << 13,
    UnlimitedSize = 1 << 14, 
    AutoTrim = 1 << 15,
    DbComputed = 1 << 16,
    RowVersion = 1 << 17,

    //System-set value, bypass authorization check
    IsSystem = 1 << 18,
    //System should not try to create/update the column definition
    AsIs = 1 << 19,
  }
  
  public enum MemberKind {
    //Persistent members 
    Column,        // Regular value-type column
    // Non persistent (transient) members; values are stored in EntityRecord.TransientValues
    Transient,    // transient, non-persistent value; a 'convenience' extra property that is not persisted
    EntityRef,    //reference to other entity
    EntityList,   //List of child entities
  }


  //Returns true if objects are equal
  public delegate bool AreValuesEqualMethod(object x, object y);
  public delegate string ValueToStringMethod(EntityMemberInfo member, object value);
  public delegate object ValueFromStringMethod(EntityMemberInfo member, string value);

  public partial class EntityMemberInfo :  HashedObject {
    public EntityMemberFlags Flags;
    public EntityInfo Entity;
    public string MemberName;
    //Copied from attributes
    public string DisplayName; 
    public string Description;
    //ClrMemberInfo might be null for hidden members added programmatically; 
    //  PropertyInfo for entity interfaces; FieldInfo or PropertyInfo for view entities
    public MemberInfo ClrMemberInfo; 
    public MemberInfo ClrClassMemberInfo;

    // Member data type - type of interface property
    public Type DataType;
    public int Size;
    public byte Precision;
    public byte Scale; 

    public MemberKind Kind; 
    public int Index; //index in Entity.Members list
    public int ValueIndex; // value index in either OriginalValues/ModifiedValues or MemoryOnlyValues
    public AutoType AutoValueType;
    public List<Attribute> Attributes = new List<Attribute>(); //attributes on property and on the same properties in buddy types

    //Get/set value handlers
    public Func<EntityRecord, EntityMemberInfo, object> GetValueRef;
    public Action<EntityRecord, EntityMemberInfo, object> SetValueRef;
    // Value converter for serialization/deserialization and for URLs
    public ValueToStringMethod ValueToStringRef;
    public ValueFromStringMethod ValueFromStringRef;
    // Comparison method
    public AreValuesEqualMethod AreValuesEqual; 

    //From ColumnAttribute
    public string ColumnName; //assigned by Column attribute, to specify explicit non-default name for table column
    public string ColumnDefault;

    // DbType, typespec specified explicitly in Column attribute
    public DbType? ExplicitDbType = null;
    public string ExplicitDbTypeSpec;


    //Specified by OldNames attribute, to handle schema change when renaming properties/columns
    public string[] OldNames; 

    //Meta-data for various kinds
    //For member Kind = EntityRef: 
    public EntityReferenceInfo ReferenceInfo;
    //For member Kind = ForeignKey 
    public EntityMemberInfo ForeignKeyOwner; //Entity ref member that owns this fk member
    //For member Kind = EntityList
    public ChildEntityListInfo ChildListInfo;

    // initial, default value
    public object DefaultValue;
    //Value returned when access denied
    public object DeniedValue;

    public EntityMemberInfo[] DependentMembers; //array of (computed) members that depend on this member


    public EntityMemberInfo(EntityInfo entity, MemberKind kind, PropertyInfo property) :
          this(entity, kind, property.Name, property.PropertyType){
      ClrMemberInfo = property; 
    }

    public EntityMemberInfo(EntityInfo entity, MemberKind kind, string name, Type dataType) {
      Entity = entity;
      Kind = kind;
      MemberName = ColumnName = DisplayName = name;
      DataType = dataType;
      if (DataType.IsNullableValueType())
        Flags |= EntityMemberFlags.Nullable;
      switch (Kind) {
        case MemberKind.EntityRef: 
          Entity.RefMembers.Add(this); 
          break;
      }
      //Set to nullable if it is Nullable generic
      if (DataType == typeof(decimal) || DataType == typeof(decimal?)) {
        this.Precision = 18; //defaults
        this.Scale = 4;
      }
      this.AreValuesEqual = MemberValueGettersSetters.AreObjectsEqual;
      //Assign default get/set handlers and to-from string converters, and comparer method
      MemberValueGettersSetters.AssignDefaultGetSetHandlers(this);
      StringConverters.AssignStringConverters(this);
      Entity.AddMember(this);
    }

    public override string ToString() {
      return StringHelper.SafeFormat("{0}.{1}({2})", Entity.Name, MemberName, DataType); 
    }

    public T GetAttribute<T>() where T : Attribute {
      return (T)Attributes.Find(a => a is T);
    }
    public bool HasAttribute<T>() where T : Attribute {
      var attr = GetAttribute<T>();
      return attr != null; 
    }

    internal void AddAttributes(IEnumerable attributes) {
      //We can't use AddRange because we have object[] array
      foreach (Attribute attr in attributes)
        Attributes.Add(attr); 
    }

  }//class

  // Container member+order info, for ordered column lists in attributes: Index("LastName:desc,FirstName:asc"); 
  public class EntityKeyMemberInfo {
    public readonly EntityMemberInfo Member;
    public readonly bool Desc;

    public EntityKeyMemberInfo(EntityMemberInfo member, bool desc) {
      Member = member;
      Desc = desc;
    }
    public override string ToString() {
      return Member.MemberName + (Desc ? " DESC" : string.Empty);
    }
  }

  [Flags]
  public enum KeyType {
    PrimaryKey = 1,
    ForeignKey = 1 << 1,
    Index = 1 << 2,

    Unique = 1 << 4,
    Clustered = 1 << 5,
    Auto = 1 << 6, // auto-created by db server; ex: Index on auto-increment column created by MySQL 

    UniqueIndex = Unique | Index,
    ClusteredIndex = Clustered | Index,
    UniqueClusteredIndex = Unique | Clustered | Index,
    ClusteredPrimaryKey = Clustered | PrimaryKey,
  }

  public enum EntityKind {
    Table,
    View
  }

  public class EntityKeyInfo : HashedObject {
    public string Name;
    public string Alias;
    public EntityInfo Entity;
    public KeyType KeyType;
    //Original members specified in attributes; may contain members that are entity references
    public List<EntityKeyMemberInfo> KeyMembers = new List<EntityKeyMemberInfo>();
    //Often the same as Members, except entity reference members had been "expanded" - replaced with value members that constitute the foreign key
    public List<EntityKeyMemberInfo> ExpandedKeyMembers = new List<EntityKeyMemberInfo>();
    public List<EntityMemberInfo> IncludeMembers = new List<EntityMemberInfo>();
    public List<EntityMemberInfo> ExpandedIncludeMembers = new List<EntityMemberInfo>();
    public string Filter;

    internal bool IsExpanded; //used in member expansion algorithm
    public bool HasIdentityMember;
    public EntityCommand SelectByKeyCommand;
    public EntityCommand SelectByKeyArrayCommand;
    public EntityMemberInfo OwnerMember; //for FK, the ref member
    public Delegate CacheSelectMethod; // compiled method to select in cache

    public EntityKeyInfo(string keyName, KeyType keyType, EntityInfo entity, EntityMemberInfo ownerMember = null) {
      Name = keyName;
      KeyType = keyType;
      Entity = entity;
      OwnerMember = ownerMember;
      entity.Keys.Add(this);
    }
    
    public override string ToString() {
      return StringHelper.SafeFormat("{0}:{1}({2})", Entity.FullName, KeyType, string.Join(",", KeyMembers));
      
    }

    public string GetMemberNames(string separator = "", bool removeUnderscore = false) {
      string result;
      if (ExpandedKeyMembers.Count == 1)
        result = ExpandedKeyMembers[0].Member.MemberName; 
      else 
        result = string.Join(separator, ExpandedKeyMembers.Select(m => m.Member.MemberName));
      if (removeUnderscore)
        result = result.Replace("_", string.Empty);
      return result; 
    }

  }//class

  public class EntityReferenceInfo :  HashedObject {
    public EntityMemberInfo FromMember;
    public EntityKeyInfo FromKey; 
    public EntityKeyInfo ToKey;
    public string ForeignKeyColumns;
    public EntityMemberInfo TargetListMember; // Entity list member on target entity based on the reference; might be null
    public LinqCommandInfo CountCommand; //counts child records for a given parent - used by CanDelete() method
    //Permission granted by reference
    public Vita.Entities.Authorization.Runtime.UserRecordPermission ByRefPermissions;

    public EntityReferenceInfo(EntityMemberInfo fromMember, EntityKeyInfo fromKey, EntityKeyInfo toKey) {
      FromMember = fromMember;
      FromKey = fromKey;
      ToKey = toKey;
      ToKey.Entity.Flags |= EntityFlags.IsReferenced;
      var fromEnt = fromMember.Entity;
      //if it is ref from table, register incoming reference
      if (fromMember.Entity.Kind == EntityKind.Table)
        ToKey.Entity.IncomingReferences.Add(fromMember);
      if (fromEnt == ToKey.Entity)
        fromEnt.Flags |= EntityFlags.SelfReferencing;
    }
  }

  public enum EntityRelationType {
    ManyToOne,
    ManyToMany
  }

  public class ChildEntityListInfo :  HashedObject {
    public EntityMemberInfo OwnerMember; 
    public EntityRelationType RelationType;
    public EntityInfo TargetEntity; //entity in the list
    public EntityInfo LinkEntity; //many-to-many only, link entity
    //One-to-many: reference member in child entity that references back to "this" entity (that owns the entity list)
    //Many-to-many: reference member in link entity that references back to "this" entity
    public EntityMemberInfo ParentRefMember;
    //Many-to-many: a member of link entity that references the "other" entity 
    public EntityMemberInfo OtherEntityRefMember;
    //for one2many - child entities; many2many - selects link entities
    public EntityCommand SelectDirectChildList;
    public EntityCommand SelectTargetListManyToMany;
    public IList<EntityKeyMemberInfo> OrderBy;
    public EntityMemberInfo PersistentOrderMember; //set if owner property has [PersistOrderIn] attribute.
    public string Filter; 

    public ChildEntityListInfo(EntityMemberInfo ownerMember) {
      OwnerMember = ownerMember; 
    }
  }

  //Used in entity replacement setup
  public class EntityReplacementInfo {
    public Type ReplacedType;
    public Type NewType;
  }


  public class ViewDefinition : HashedObject {
    public EntityModule Module;
    public Type EntityType;
    public LinqCommand Command;
    public DbViewOptions Options;
    public string Name;

    public ViewDefinition(EntityModule module, Type entityType, EntityQuery query, DbViewOptions options, string name) {
      Module = module;
      EntityType = entityType;  
      Options = options; 
      Name = name;
      Command = new LinqCommand(query, LinqCommandType.Select, LinqCommandKind.View, null);
    }
  }

  public class SequenceDefinition : HashedObject {
    public EntityModule Module;
    public string Name;
    public Type DataType;
    public int StartValue;
    public int Increment;
    public string ExplicitSchema; //if provided, used instead of module.area.schema

    public SequenceDefinition(EntityModule module, string name, Type dataType, int startValue = 0, int increment = 0, 
           string schema = null) {
      Module = module;
      Name = name;
      DataType = dataType;
      StartValue = startValue;
      Increment = increment;
      ExplicitSchema = schema;
    }
  }


}//namespace
