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
    public List<EntityMemberInfo> Members;
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

    public EntityEvents Events { get; private set; }
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
      Members = new List<EntityMemberInfo>();
      Events = new EntityEvents(); 
      //Check for generic types - happens in modules with generic entities (interfaces), provided for customization
      if(Name.Contains('`'))
        Name = Name.Substring(0, Name.IndexOf('`'));
      if(EntityType.GetTypeInfo().IsInterface && Name.Length > 1 && Name.StartsWith("I"))
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

    ICollection<EntityMemberInfo> _membersColl; 
    public ICollection<EntityMemberInfo> GetMembers() {
      return _membersColl = _membersColl ?? new Collection<EntityMemberInfo>(this.Members.OfType<EntityMemberInfo>().ToList()); 
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

    public string MemberName { get; set; }
    // Member data type - type of interface property
    public Type DataType { get; private set; }
    public int Size { get; set; }
    public byte Precision;
    public byte Scale; 

    public EntityMemberKind Kind; 
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
    //For member Kind = ForeignKey 
    public EntityMemberInfo ForeignKeyOwner; //Entity ref member that owns this fk member
    //For member Kind = EntityList
    public ChildEntityListInfo ChildListInfo;
    //Permission granted by reference
    public object ByRefPermissions;

    // initial, default value
    public object DefaultValue;
    //Value returned when access denied
    public object DeniedValue;

    public EntityMemberInfo[] DependentMembers; //array of (computed) members that depend on this member

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

  public enum EntityKind {
    Table,
    View
  }

  public class EntityFilter {
    public StringTemplate Template;
    public List<EntityMemberInfo> Members; 
  }

  public class EntityKeyInfo {
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
    public EntityKeyInfo IsCopyOf; //for FKs

    public bool HasIdentityMember;
    public EntityMemberInfo OwnerMember; //for FK, the ref member
    public Delegate CacheSelectMethod; // compiled method to select in cache
    public EntityFilter IndexFilter;
    public string ExplicitDbKeyName; 

    public EntityKeyInfo(EntityInfo entity, KeyType keyType, 
        EntityMemberInfo ownerMember = null, string alias = null) {
      KeyType = keyType;
      Entity = entity;
      OwnerMember = ownerMember;
      Alias = alias; 
      entity.Keys.Add(this);
    }
    
    public override string ToString() {
      return Util.SafeFormat("{0}:{1}({2})", Entity.FullName, KeyType, string.Join(",", KeyMembers));
    }
    public bool IsExpanded() {
      return ExpandedKeyMembers.Count > 0; 
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

    public string GetFullRef() {
      return this.Name + "(" + string.Join(",", KeyMembers.Select(km => km.Member.MemberName)) + ")";
    }


  }//class

  public class EntityReferenceInfo {
    public EntityMemberInfo FromMember;
    public EntityKeyInfo FromKey { get; private set; } 
    public EntityKeyInfo ToKey { get; private set; }
    public string ForeignKeyColumns;
    public EntityMemberInfo TargetListMember; // Entity list member on target entity based on the reference; might be null
    public LinqCommand CountCommand; //counts child records for a given parent - used by CanDelete() method

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

    public EntityFilter Filter;
    public List<EntityKeyMemberInfo> OrderBy;

    LinqCommand _selectDirectChildRecordsCommand;

    public ChildEntityListInfo(EntityMemberInfo ownerMember) {
      OwnerMember = ownerMember;
    }

    public LinqCommand GetSelectDirectChildRecordsCommand() {
      if (_selectDirectChildRecordsCommand == null) {
        var mask = this.ParentRefMember.Entity.AllMembersMask;
         var cmdInfo = SelectCommandBuilder.BuildSelectByKey(this.ParentRefMember.ReferenceInfo.FromKey, mask, 
                                                    Locking.LockType.None, OrderBy);
        if(RelationType == EntityRelationType.ManyToMany) {
          cmdInfo.Includes = new List<LambdaExpression>();
          var include = ExpressionMaker.MakeInclude(this.LinkEntity.EntityType, this.OtherEntityRefMember);
          cmdInfo.Includes.Add(include);
        }
        _selectDirectChildRecordsCommand = cmdInfo;
      } // if 
      return _selectDirectChildRecordsCommand;
    }

  }



}//namespace
