using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Model{

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

    ReferencesIdentity = 1 << 19,

    // Do not track in TransactionLog
    DoNotTrack = 1 << 25,

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
    DbComputedExpression = 1 << 16,
    RowVersion = 1 << 17,

    //System-set value, bypass authorization check
    IsSystem = 1 << 18,
    //System should not try to create/update the column definition
    AsIs = 1 << 19,

    FromOneToOneRef = 1 << 20,

  }

  public enum EntityMemberKind {
    //Persistent members 
    Column,        // Regular value-type column
    // Non persistent (transient) members; values are stored in EntityRecord.TransientValues
    EntityRef,    //reference to other entity
    EntityList,   //List of child entities
    Transient,    // transient, non-persistent value; a a property that is not persisted
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


}
