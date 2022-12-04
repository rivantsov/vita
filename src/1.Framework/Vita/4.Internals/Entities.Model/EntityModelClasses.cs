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

using Vita.Entities.Utilities;
using System.Collections.ObjectModel;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Data.Linq;
using Vita.Entities.Model.Construction;
using Vita.Entities.Locking;

namespace Vita.Entities.Model {


  public delegate string RecordDisplayMethod(EntityRecord record);

  /// <summary>Pseudo entity representing DB context. Used in queries that do not query tables like 'Select SysDataTime()'. </summary>
  public interface INullEntity { }

  //contains information about generated class
  public class EntityClassInfo {
    public Type Type; 
    public Func<EntityRecord, EntityBase> CreateInstance;
  }

  /// <summary>Identifies type of cache that should be used for an entity. </summary>
  public enum EntityCachingType {
    None,
    LocalSparse,
    Sparse,
    FullSet,
  }

  // We use entity Model objects as keys in DbModel's tables(dictionaries) of all db objects - so they all derive from HashedObject.
  // For example, to find a table for an entity (find DbTableInfo for an EntityInfo). 
  // Note that there may be multiple DbModels for a single entity Model, so we may have a different DB object in each DbModel for an entity object.

  public class EntityInfo {
    public readonly EntityModule Module;
    public EntityArea Area;
    public string Name;
    public string FullName { get; private set; }
    public string TableName; // explicitly set table name in Entity attribute
    public EntityKind Kind;
    //view
    public ViewDefinition ViewDefinition;
    //public LinqCommand ViewCommand; 

    public readonly Type EntityType;
    public readonly HashSet<Type> ReplacesTypes = new HashSet<Type>(); //in case of entity replacement, the types that this type is replacing
    public EntityInfo ReplacedBy;
    public List<EntityMemberInfo> Members = new List<EntityMemberInfo>();
    public readonly List<EntityKeyInfo> Keys = new List<EntityKeyInfo>(); 
    public EntityKeyInfo PrimaryKey;

    public Dictionary<string, EntityMemberInfo> MembersByName 
          = new Dictionary<string, EntityMemberInfo>(StringComparer.OrdinalIgnoreCase);
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
    public EntityCachingType CachingType;

    public EntityEvents Events { get; private set; } = new EntityEvents(); 
    public readonly EntitySaveEvents SaveEvents = new EntitySaveEvents();
    internal object[] InitialColumnValues;


    //Topological sorting index, assigned by SCC (Strongly-Connected Components) algorithm
    public int TopologicalIndex;
    internal Utilities.SccGraph.Vertex SccVertex; //used in SCC algorithm

    //Entity classes information
    public EntityClassInfo ClassInfo;

    public List<EntityKeyMemberInfo> DefaultOrderBy;

    public string[] OldNames; //From OldNames attribute 

    public EntityMemberInfo IdentityMember;
    public EntityMemberInfo RowVersionMember;

    public ConstantExpression EntitySetConstant;

    public EntityMemberMask AllMembersMask; 

    public EntityInfo(EntityModule module, Type entityType, EntityKind kind = EntityKind.Table, EntityArea altArea = null) {
      Module = module;
      EntityType = entityType;
      Area = altArea ?? Module.Area;
      Kind = kind; 
      Name = entityType.Name;
      //Check for generic types - happens in modules with generic entities (interfaces), provided for customization
      if(Name.Contains('`'))
        Name = Name.Substring(0, Name.IndexOf('`'));
      if(EntityType.IsInterface && Name.Length > 1 && Name.StartsWith("I"))
        Name = Name.Substring(1);
      FullName = Area.Name + "." + Name;
      EntitySetConstant = ExpressionMaker.MakeEntitySetConstant(this.EntityType); 
    }

    // Used for INullEntity, fake entity for special queries
    internal EntityInfo(Type entityType) {
      EntityType = entityType;
      Kind = EntityKind.Table;
      Name = entityType.Name;
      Members = new List<EntityMemberInfo>();
      Events = new EntityEvents();
      FullName = Name; 
    }

    public override string ToString() {
      return EntityType.ToString();
    }
    public override int GetHashCode() {
      return Name.GetHashCode();
    }

    public EntityMemberInfo GetMember(string name, bool throwIfNotFound = false) {
      if(name != null)
        name = name.Trim().ToLowerInvariant(); 
      EntityMemberInfo member;
      if(MembersByName.TryGetValue(name, out member)) 
        return member;
      if (throwIfNotFound)
        throw new Exception(Util.SafeFormat("Member {0} not found in entity {1}.", name, EntityType.Name));
      return null; 
    }

    public EntityMemberInfo FindMemberOrColumn(string name) {
      name = name.Trim();
      return Members.FirstOrDefault(m => name.Equals(m.MemberName, StringComparison.OrdinalIgnoreCase) ||
                                       name.Equals(m.ColumnName, StringComparison.OrdinalIgnoreCase));
    }

    public EntityMemberInfo GetMember<TEntity>(Expression<Func<TEntity, object>> propertySelector) {
      var propName = ExpressionHelper.GetSelectedProperty(propertySelector);
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

  

  //Returns true if objects are equal
  public delegate bool AreValuesEqualMethod(object x, object y);
  public delegate string ValueToStringMethod(EntityMemberInfo member, object value);
  public delegate object ValueFromStringMethod(EntityMemberInfo member, string value);

  public partial class EntityMemberInfo {
    public EntityMemberFlags Flags;
    public EntityInfo Entity;
    //Copied from attributes
    public string DisplayName; 
    public string Description;
    //ClrMemberInfo might be null for hidden members added programmatically; 
    //  PropertyInfo for entity interfaces; FieldInfo or PropertyInfo for view entities
    public MemberInfo ClrMemberInfo; 
    public MemberInfo ClrClassMemberInfo;
    public readonly List<EntityKeyInfo> UsedByKeys = new();
    // for FK expanded column: the owner ref member that references entity with Identity that produces this value
    public EntityMemberInfo OwnerIdSourceRefMember; 


    public string MemberName { get; set; }
    // Member data type - type of interface property
    public Type DataType { get; private set; }
    public int Size { get; set; }
    public byte Precision;
    public byte Scale; 

    public EntityMemberKind Kind;
    public DbComputedKindExt ComputedKind; 
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

    public string ExplicitDbTypeSpec;


    //Specified by OldNames attribute, to handle schema change when renaming properties/columns
    public string[] OldNames; 

    //Meta-data for various kinds
    //For member Kind = EntityRef: 
    public EntityReferenceInfo ReferenceInfo;
    //For member Kind = EntityList
    public ChildEntityListInfo ChildListInfo;
    //Permission granted by reference
    public object ByRefPermissions;

    // initial, default value
    public object DefaultValue;
    //Value returned when access denied
    public object DeniedValue;

    public EntityMemberInfo[] DependentMembers; //array of (computed) members that depend on this member
    
    // Deprecated! member may be in multiple FKs
    //For member Kind = ForeignKey 
    //public EntityMemberInfo ForeignKeyOwner; //Entity ref member that owns this fk member


    public EntityMemberInfo(EntityInfo entity, EntityMemberKind kind, PropertyInfo property) :
          this(entity, kind, property.Name, property.PropertyType) {
      ClrMemberInfo = property;
    }

    public EntityMemberInfo(EntityInfo entity, EntityMemberKind kind, string name, Type dataType) {
      Entity = entity;
      Kind = kind;
      MemberName = ColumnName = DisplayName = name;
      DataType = dataType;
      if(DataType.IsNullableValueType())
        Flags |= EntityMemberFlags.Nullable;
      //Set to nullable if it is Nullable generic
      if(DataType == typeof(decimal) || DataType == typeof(decimal?)) {
        this.Precision = 18; //defaults
        this.Scale = 4;
      }
      this.AreValuesEqual = MemberValueGettersSetters.AreObjectsEqual;
      //Assign default get/set handlers and to-from string converters, and comparer method
      MemberValueGettersSetters.AssignDefaultGetSetHandlers(this);
      MemberStringConverters.AssignStringConverters(this);
      Entity.AddMember(this);
    }

    public override string ToString() {
      return Util.SafeFormat("{0}.{1}({2})", Entity.Name, MemberName, DataType); 
    }
    public override int GetHashCode() {
      return MemberName.GetHashCode();
    }

    public T GetAttribute<T>() where T : Attribute {
      return (T)Attributes.Find(a => a is T);
    }
    public bool HasAttribute<T>() where T : Attribute {
      var attr = GetAttribute<T>();
      return attr != null; 
    }

    public string FullName {
      get { return $"{Entity.Name}.{MemberName}"; }
    }

  }//class

  // Container member+order info, for ordered column lists in attributes: Index("LastName:desc,FirstName:asc"); 
  public class EntityKeyMemberInfo {
    public string MemberName; // may come from key spec, when there's no member yet
    public EntityMemberInfo Member;
    public bool Desc;

    public EntityKeyMemberInfo(EntityMemberInfo member, bool desc = false) {
      Member = member;
      Desc = desc;
      MemberName = member.MemberName;
    }

    //used when member comes from KeySpec
    public EntityKeyMemberInfo(string memberName, EntityMemberInfo member = null, bool desc = false) {
      Desc = desc;
      MemberName = memberName;
    }


    public override string ToString() {
      return MemberName + (Desc ? " DESC" : string.Empty);
    }
  }

  public enum EntityKind {
    Table,
    View
  }

  public class EntityFilterTemplate {
    public StringTemplate Template;
    public List<EntityMemberInfo> Members; 
  }

  /// <summary>Encodes the phases of building the EntityKeyInfo instance for a key. </summary>
  public enum KeyMembersStatus {
    /// <summary>Initial state </summary>
    None,

    /// <summary>KeyMembers list is created (from keyspec), but not all references to actual entity members are assigned </summary>
    Listed,

    // Key assignment. We may have situation when key members/columns are specified in member list
    // (ex: PK on entity with list of members, not on single member)
    //  But some members/columns are 'hidden' initially, they are produced from EntityRef with explicit
    //  column names. So initially we list KeyMembers with names only, possibly without actuall ref to member
    //  Then we do assignment - when we try to put actual refs to members based on name

    /// <summary>For all KeyMembers the field Member is assigned </summary>
    Assigned,

    /// <summary>KeyMembers are copied to KeyMembersExpanded with expansion of entity-type members. 
    /// For ex, Customer in KeyMembers is translated into CustomerId in KeyMembersExpanded.</summary>
    Expanded,

  }

  public class EntityKeyInfo {
    public string Name;
    public string Alias;
    public EntityInfo Entity;
    public KeyType KeyType;
    // source attributes, might be null
    public KeyAttribute SourceKeyAttribute;
    public EntityRefAttribute SourceRefAttribute;
    public EntityMemberInfo OwnerMember;
    public string ExplicitDbKeyName;

    public EntityKeyInfo TargetUniqueKey; //for FKs

    public string KeyMembersSpec; //ex for index: "price:desc;brand,prodName"
    //Original members specified in attributes; may contain members that are entity references
    public List<EntityKeyMemberInfo> KeyMembers = new List<EntityKeyMemberInfo>();
    // Entity reference members had been "expanded" - replaced with value members that constitute the foreign key
    public List<EntityKeyMemberInfo> KeyMembersExpanded = new List<EntityKeyMemberInfo>();
    // Include members
    public string IncludeMembersSpec; 
    public List<EntityMemberInfo> IncludeMembers = new List<EntityMemberInfo>();
    public List<EntityMemberInfo> IncludeMembersExpanded = new List<EntityMemberInfo>();

    // for indexes with filter
    public string FilterSpec;
    public EntityFilterTemplate ParsedFilterTemplate;

    public KeyMembersStatus MembersStatus;
    public bool HasIdentityMember;
    public Delegate CacheSelectMethod; // compiled method to select in cache

    // cached keys, set on first use
    internal string SqlCacheKey_SelectByPkNoLock;
    internal string SqlCacheKey_ChildExists;

    public EntityKeyInfo(EntityInfo entity, KeyType keyType, EntityMemberInfo ownerMember = null, 
                         KeyAttribute sourceKeyAttr = null, EntityRefAttribute sourceRefAttr = null) {
      KeyType = keyType;
      Entity = entity;
      OwnerMember = ownerMember; 
      Alias = SourceKeyAttribute?.Alias;
      ExplicitDbKeyName = sourceKeyAttr?.DbKeyName;
      entity.Keys.Add(this);
    }

    public bool IsExpanded() {
      return MembersStatus >= KeyMembersStatus.Expanded;
    }

    public override string ToString() {
      return Util.SafeFormat("{0}:{1}({2})", Entity.FullName, KeyType, GetMembersListAsString());
    }

    public string GetFullRef() {
      return this.Name + "(" + GetMembersListAsString() + ")";
    }
    public string GetMembersListAsString() {
      return string.Join(",", KeyMembers.Select(km => km.MemberName));
    }

    public static IList<EntityKeyInfo> EmptyList = new EntityKeyInfo[] { };

  }//class

  public class EntityReferenceInfo {
    public EntityMemberInfo FromMember;
    public EntityKeyInfo FromKey { get; private set; } 
    public EntityKeyInfo ToKey { get; private set; }
    public string ForeignKeyColumns;
    public EntityMemberInfo TargetListMember; // Entity list member on target entity based on the reference; might be null

    public EntityReferenceInfo(EntityMemberInfo fromMember, EntityKeyInfo fromKey, EntityKeyInfo toKey) {
      FromMember = fromMember;
      Util.CheckParam(fromKey, nameof(fromKey));
      Util.CheckParam(toKey, nameof(toKey));
      FromKey = fromKey;
      ToKey = toKey;
      ToKey.Entity.Flags |= EntityFlags.IsReferenced;
      var fromEnt = FromMember.Entity;
      //if it is ref from table, register incoming reference
      if(FromMember.Entity.Kind == EntityKind.Table)
        ToKey.Entity.IncomingReferences.Add(FromMember);
      if(fromEnt == ToKey.Entity)
        fromEnt.Flags |= EntityFlags.SelfReferencing;
    }

  }

  public enum EntityRelationType {
    ManyToOne,
    ManyToMany
  }

  public class ChildEntityListInfo {
    public EntityMemberInfo OwnerMember; 
    public EntityRelationType RelationType;
    public EntityInfo TargetEntity; //entity in the list
    public EntityInfo LinkEntity; //many-to-many only, link entity
    //One-to-many: reference member in child entity that references back to "this" entity (that owns the entity list)
    //Many-to-many: reference member in link entity that references back to "this" entity
    public EntityMemberInfo ParentRefMember;
    //Many-to-many: a member of link entity that references the "other" entity 
    public EntityMemberInfo OtherEntityRefMember;
    public EntityMemberInfo PersistentOrderMember; //set if owner property has [PersistOrderIn] attribute.

    public EntityFilterTemplate Filter;
    public List<EntityKeyMemberInfo> OrderBy;

    internal string SqlCacheKey_SelectChildRecs;
    internal string SqlCacheKey_SelectChildRecsForInclude;

    public ChildEntityListInfo(EntityMemberInfo ownerMember) {
      OwnerMember = ownerMember;
    }

  }



}//namespace
