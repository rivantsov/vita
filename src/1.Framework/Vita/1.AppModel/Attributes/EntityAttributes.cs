using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Vita.Data.Driver;
using Vita.Entities.Model;
using Vita.Entities.Model.Construction;

namespace Vita.Entities {

  /// <summary>Marks an interface as an Entity. </summary>
  [AttributeUsage(AttributeTargets.Interface)]
  public partial class EntityAttribute: EntityModelAttributeBase {
    public string Name { get; set; }
    public string PluralName { get; set; }
    public string TableName { get; set; }
  }

  /// <summary>Provides information (column name) about underlying database column for an entity property. Optional.</summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class ColumnAttribute: EntityModelAttributeBase {
    public string ColumnName { get; set; }
    public string Default { get; set; }
    public int Size { get; set; }
    public byte Scale { get; set; }
    public byte Precision { get; set; }
    public bool AnsiString { get; set; }

    public string DbTypeSpec;

    public ColumnAttribute() { }
    public ColumnAttribute(string columnName) {
      ColumnName = columnName; 
    }
  }

  /// <summary>Identifies a member (which must be an entity list) as referencing other entity set through many-to-many relationship.
  /// Specifies the "link" entity that implements many-to-many linking. </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class ManyToManyAttribute: EntityModelAttributeBase {
    public ManyToManyAttribute(Type linkEntity) { LinkEntity = linkEntity; }
    
    /// <summary>The type of "link" entity (table) that implements many-to-many linking. Required.</summary>
    public Type LinkEntity { get; set; }
    
    /// <summary>Specifies the property name on the link entity that references "this" entity. Optional. </summary>
    /// <remarks>If this attribute is not present, the system automatically finds the member by its type. 
    /// Use this attribute if there is more than one member on the target entity that points to the "other" entity.</remarks>
    public string ThisEntityRef { get; set; }

    /// <summary>Specifies the property name on the link entity that references the "other" entity in many-to-many relationship. Optional. </summary>
    /// <remarks>If this attribute is not present, the system automatically finds the member by its type. 
    /// Use this attribute if there is more than one member on the target entity that points back to this entity.</remarks>
    public string OtherEntityRef { get; set; }

  }

  /// <summary>Marks an entity reference, optional. Might be used to specify the names of the foreign key columns. </summary>
  ///<remarks>The primary use is for reverse-engineering tool (database to c# code generator) to explicitly identify the foreign key columns in the database table.
  ///</remarks> 
  [AttributeUsage(AttributeTargets.Property)]
  public partial class EntityRefAttribute : EntityModelAttributeBase {
    public string KeyColumns;
    public string ForeignKeyName;
    public string TargetUniqueIndexAlias;

    public EntityRefAttribute(string keyColumns = null) {
      KeyColumns = keyColumns == null ? null : keyColumns.Replace(" ", string.Empty);
    }

  }

  /// <summary>Identifies an entity-type property as derived from one-to-one relation. The relation itself is 
  /// defined by a reference property on child entity that is also a primary key.  </summary>
  /// <remarks>
  /// <para>Put this attribute on parent's property referencing a child entity 
  /// that has a primary key based on back reference to this parent. </para>
  /// <para>Example: entities: IParty, IPerson, IOrg are in one-to-one relationship. 
  /// Proprty IPerson.Party is a primary key for IPerson, same for IOrg.Party. 
  /// IParty has 2 properties marked with [OneToOne] attribute: IParty.Person, IParty.Org.
  /// These properties do not have supporting columns in IParty, their values 
  /// are derived from back references: IPerson.Party, IPerson.Org.</para>
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class OneToOneAttribute : EntityModelAttributeBase {
  }


  /// <summary>Identifies the many-to-one relationship and provides additonal information about it. Optional. </summary>
  /// <remarks>
  /// Placed on entity-list property (of parent entity) in one-to-many relationship. 
  /// Optional; use it when you need to explicitly specify the property (foreign key) on child entity that points back to "this" entity.
  /// For example, there may be more than one property that points back to this entity - in this case you need to specify the property
  /// name explicitly using this attribute.
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class OneToManyAttribute : EntityModelAttributeBase {
    // public string Filter; // not implemented in .NET Core version (yet)
    public OneToManyAttribute(string thisEntityRef = null) {
      ThisEntityRef = thisEntityRef; 
    }
    
    /// <summary>The name of the property on the target (list member) entity that points back to "this" property 
    /// in one-to-many relationships.</summary>
    public string ThisEntityRef { get; set; }
  }


  /// <summary>
  /// Specifies that the order of elements in the list is explicit and should be maintained automatically
  /// using the designated property in the link entity (many-to-many) or in the target entity (one-to-many).
  /// </summary>
  /// <remarks>The target entity or link entity must have OrderBy attribute specifying the target "ordering" property.
  /// </remarks>
  // TODO: this is a current limitation, that target entity should be ordered by the property referenced in PersistOrderIn
  // attribute. Need to remove this, and allow have different OrderBy's
  [AttributeUsage(AttributeTargets.Property)]
  public partial class PersistOrderInAttribute: EntityModelAttributeBase {
    public string Property { get; set; }
    public PersistOrderInAttribute(string property) {
      Property = property; 
    }
  }

  /// <summary> Specifies the default sort order for an entity list when multiple entities are loaded from database.</summary>
  /// <remarks>
  /// Use this attribute to specify the sort order of entity lists. Provide a list of property names
  /// delimited by comma, with optional direction specifier (ASC/DESC), like "LastName:ASC,FirstName:DESC".
  /// If specifier is missing, ASC is assumed. The attribute can be used on entities only.
  /// In the future, I hope to add support for it on entity list properties.
  /// </remarks>
  [AttributeUsage(AttributeTargets.Interface|AttributeTargets.Property)]
  public partial class OrderByAttribute : EntityModelAttributeBase {
    public string OrderByList;
    public OrderByAttribute(string orderByList) {
      OrderByList = orderByList;
    }
  }  

  /// <summary> Specifies the old name(s) for a column or table. </summary>
  /// <remarks>
  /// Use this attribute when renaming an entity or property to specify the old name for table or column. 
  /// System needs the old name to match the existing database object with new entity or property, to rename 
  /// the database object. Renaming might happen more than once, so you can specify more than one name delimited by comma.
  /// The attribute may stay for a while, after renaming happened and propagated to all machines - if an object 
  /// (property or table) matches existing object in database, this attribute has no effect. 
  /// </remarks>
  [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property)]
  public partial class OldNamesAttribute: EntityModelAttributeBase {
    public string OldNames;
    public OldNamesAttribute(string oldNames) {
      OldNames = oldNames;
    }
  }

  /// <summary>Defines a property group.</summary>
  [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
  public partial class PropertyGroupAttribute: EntityModelAttributeBase {
    public string GroupName;
    public string MemberNames;
    public PropertyGroupAttribute(string groupName, string memberNames) {
      GroupName = groupName;
      MemberNames = memberNames; 
    }
  }

  /// <summary>Specifies property group(s) for a property </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class GroupsAttribute: EntityModelAttributeBase {
    public string GroupNames;
    public GroupsAttribute(string groupNames) {
      GroupNames = groupNames; 
    }
  }

  /// <summary> Identifies a primary key. Can be placed on a property (without parameters), 
  /// or on an entity with a list of column names as parameter.
  /// </summary>
  [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property)]
  public partial class PrimaryKeyAttribute: KeyAttribute {
    public bool Clustered;

    public PrimaryKeyAttribute() : base(KeyType.PrimaryKey) { }

    /// <summary>Constructor with explicit key members list, with optional asc/desc flags.</summary>
    /// <param name="membersSpec">Members specification. Ex: 'Name-asc;DateCreated-desc;ParentId'</param>
    /// <remarks>You can use entity property names, or column names, 
    ///   for ex: foreign key columns for an entity ref member.</remarks>
    public PrimaryKeyAttribute(string membersSpec) : base(KeyType.PrimaryKey) {
      MembersSpec = membersSpec;
    }
  }

  /// <summary>
  /// Specifies a non-unique index on a database table. 
  /// Can be placed on a property (without parameters), or on an entity with a list of column names as parameter.
  /// </summary>
  [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property, AllowMultiple = true)]
  public partial class IndexAttribute: KeyAttribute {
    public bool Clustered;
    public bool Unique;
    public string IncludeMembers; //list of members to additionally 'include' into index
    public string Filter; //filtering expression; member/column references should be enclosed in [..] brackets

    public IndexAttribute() : this(null) { }
    public IndexAttribute(string memberNames) : base(KeyType.Index) {
      base.MembersSpec = memberNames;
    }

  }

  /// <summary> Specifies a unique clustered index on a database table. Can be placed on a property (without parameters), 
  /// or on an entity with a list of column names as parameter. </summary>
  [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property)]
  public class UniqueClusteredIndexAttribute : IndexAttribute {
    /// <summary> Parameterless constructor, for use on property.</summary>
    public UniqueClusteredIndexAttribute() : this(null) { }
    /// <summary>For use on Entity. </summary>
    /// <param name="propertyOrColumnNames">Comma-delimited list of property or column names.</param>
    public UniqueClusteredIndexAttribute(string propertyOrColumnNames) : base(propertyOrColumnNames) {
      this.Unique = true;
      this.Clustered = true; 
    }
  }

  /// <summary> Specifies a clustered index on a database table. Can be placed on a property (without parameters), 
  /// or on an entity with a list of column names as parameter. </summary>
  [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property)]
  public partial class ClusteredIndexAttribute : IndexAttribute {
    /// <summary> Parameterless constructor, for use on property.</summary>
    public ClusteredIndexAttribute() : this(null) { }
    /// <summary>For use on Entity. </summary>
    /// <param name="propertyOrColumnNames">Comma-delimited list of property or column names.</param>
    public ClusteredIndexAttribute(string propertyOrColumnNames) : base(propertyOrColumnNames) {
      this.Clustered = true; 
    }
  }

  /// <summary>Specifies a unique index on a database table. May be placed on a property (without parameters), 
  /// or on an entity with a list of column names as parameter.</summary>
  [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property, AllowMultiple = true)]
  public class UniqueAttribute : IndexAttribute {
    public UniqueAttribute() : this(null) { }
    public UniqueAttribute(string memberNames) : base(memberNames) {
      Unique = true; 
    }
  }

  /// <summary> Specifies that entity property is non-persistent, exists only in memory and has no underlying database column. </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class NoColumnAttribute: EntityModelAttributeBase { }

  /// <summary>Specifies that entity property may not be updated after entity is inserted into database table.</summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class NoUpdateAttribute: EntityModelAttributeBase { }

  /// <summary>Specifies that entity property value may not be assigned.</summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class ReadOnlyAttribute: EntityModelAttributeBase { }

  /// <summary>Defines types of automatic values in entity properties/columns. Used by <c>Auto</c> attribute. </summary>
  public enum AutoType {
    /// <summary>No automatic value. Used internally. </summary>
    None,
    /// <summary>New GUID is assigned to the property when new entity is created.</summary>
    NewGuid,
    /// <summary>For new entities the property is set to the current datetime value.</summary>
    CreatedOn,
    /// <summary>When an entity is created or updated, the property is set to the current datetime value.</summary>
    UpdatedOn,
    /// <summary>For new entities the property is set to the name of the current user (from session.UserContext.UserName field).</summary>
    CreatedBy,
    /// <summary>When an entity is created or updated, the property value is set to the name of the current user 
    /// (from session.UserContext.UserName field).</summary>
    UpdatedBy,
    /// <summary>For new entities the property is set to the Id of the current user (from session.UserContext.UserId field).</summary>
    CreatedById,
    /// <summary>When an entity is created or updated, the property value is set to the Id of the current user 
    /// (from session.UserContext.UserId field).</summary>
    UpdatedById,
    /// <summary>For new entities the property is set to the transaction Id.</summary>
    TransIdCreated,
    /// <summary>When an entity is created or updated, the property value is set to the transaction Id.</summary>
    TransIdUpdated,
    /// <summary>The property holds an identity value generated in the database. </summary>
    Identity,
    /// <summary>The property holds an row version (timestamp) value. The value is automatically incremented when entity is updated.</summary>
    RowVersion,
    /// <summary>The value is generated by a named DB sequence. </summary>
    Sequence, 
  }

  /// <summary> Identifies automatically generated value for new records. </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class AutoAttribute: EntityModelAttributeBase {
    public readonly AutoType Type;
    public string SequenceName;
    public AutoAttribute(AutoType type = AutoType.NewGuid) {
      Type = type; 
    }
  }

  /// <summary>Should be used on properties that are entity references.
  /// Specifies that with any update of the child (this) entity the value of the column tracking the update datetime of the parent
  /// (marked with [Auto(AutoType.UpdatedOn)] attribute) must be automatically updated as well. 
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class PropagateUpdatedOnAttribute: EntityModelAttributeBase {}

    /// <summary>Signals that int property contains a hash value for other property. The hash value will be automatically 
    /// set by the system when target property is modified and record is saved.</summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class HashForAttribute: EntityModelAttributeBase {
    public readonly string PropertyName; 
    public HashForAttribute(string propertyName) {
      PropertyName = propertyName; 

    }
  }

  public enum TrackedAction {
    Created,
    Updated,
    Deactivated, //not used, might be used in the future
  }


  /// <summary>An attribute to use on Guid properties that will hold the ID of the user transaction that created or 
  /// was last to modify an entity. 
  /// </summary>
  /// <remarks>It is a common practice to add extra tracking columns like [CreatedByUser], [UpdatedByUser], 
  /// [CreatedOnDateTime] and alike to tables in the database to track the create/update actions.
  /// VITA provides an alternative - instead of adding these multiple string/date-time columns, you can track user
  /// transactions (update actions) in a separate table [UserTransaction], and add references (ID values) to table 
  /// you need to track. Add properties like CreatedInId, UpdatedInId (Guid) to entities you want to track and decorate
  /// the properties with the [TransactionId(actiontype)] attribute.
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class TransactionIdAttribute : EntityModelAttributeBase {
    public TrackedAction Action;

    public TransactionIdAttribute(TrackedAction action = TrackedAction.Updated) {
      Action = action;
    }
  }
    /// <summary> Marks identity column in the database.</summary>
    /// <remarks>Allowed on integer-type members only.</remarks>
    [AttributeUsage(AttributeTargets.Property)]
  public class IdentityAttribute : AutoAttribute {
    public IdentityAttribute() : base(AutoType.Identity) { }
  }

  /// <summary>Specifies that property value is generated automatically from DB sequence. </summary>
  /// <remarks>DB sequence is assumed to be in the same schema/area as the the module in which parent entity
  /// is defined. You can specify schema/area name explicitly using dotted notation: dbo.MySequence .</remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public class SequenceAttribute : AutoAttribute {
    public SequenceAttribute(string sequenceName) : base(AutoType.Sequence) {
      base.SequenceName = sequenceName; 
    }
  }

  /// <summary> Marks RowVersion (timestamp) column in the database.</summary>
  /// <remarks>The property must be of type ulong (UInt64).</remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public class RowVersionAttribute : AutoAttribute {
    public RowVersionAttribute() : base(AutoType.RowVersion) { }
  }

  /// <summary>
  /// Defines options for the <c>SizeAttribute</c>.
  /// </summary>
  [Flags]
  public enum SizeOptions {
    /// <summary>No options.</summary>
    None = 0,
    /// <summary>Use it with columns holding log/system-generated messages, when it's hard to control/predict the message size, 
    /// but information is critical and we should not reject it, rather trim instead.</summary>
    AutoTrim = 1, 
  }

  /// <summary>Specifies a maximum length of a char or binary column (char, nvarchar(*)) in the database. </summary>
  /// <remarks>You can specify size value directly, or your can reference the size saved in SizeTable by code.
  /// If you use size code, the system looks up the size tables in entity module and then in the entity app. 
  /// Uses codes defined in Sizes static class, or define your own codes. During module/app setup, 
  /// specify values for size codes you used. For standard sizes in Sizes class system provides default values. 
  /// This facility allows customizing entity definitions (column sizes) in imported modules at app level. 
  /// You can provide size code and default size value - default will be used if the value for size code is not specified. 
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class SizeAttribute : EntityModelAttributeBase {
    public int Size;
    public string SizeCode;
    public SizeOptions Options; 
    public SizeAttribute(int size, SizeOptions options = SizeOptions.None) {
      Size = size;
      Options = options; 
    }
    public SizeAttribute(string sizeCode, int defaultSize = 0, SizeOptions options = SizeOptions.None) {
      SizeCode = sizeCode;
      Options = options; 
      Size = defaultSize;
    }
  }

  /// <summary> Specifies that property contains date-only part.</summary>
  /// <remarks>Allowed on DateTime members only.</remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class DateOnlyAttribute : EntityModelAttributeBase {
  }

  /// <summary> Specifies that property contains UTC datetime.</summary>
  /// <remarks>Allowed on DateTime members only. When application assigns a datetime value to a property marked with [Utc] attribute, the system 
  /// automatically checks the Kind property of the DateTime value. If it is set to Local, then system converts the value to Utc. 
  /// So you do not have to worry about the proper kind of DateTime value that you assign to Utc property - the system adjusts automatically.
  /// The value returned is always Utc datetime. </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class UtcAttribute : EntityModelAttributeBase { }

  /// <summary>Marks a string or binary property as unlimited size column (nvarchar(max) in SQL Server). </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class UnlimitedAttribute: EntityModelAttributeBase { }

  /// <summary>
  /// Marks property as nullable. Can be placed on properties of type string, Entity reference or even ValueType. 
  /// </summary>
  /// <remarks>
  /// Strings and entity-type properties are not nullable by default; you have to add this attribute to make them
  /// nullable. Value types (int, Guid, etc) can also be made nullable. One way is to use the Nullable&lt;...&gt; 
  /// derived types (like "int?") for property types. In this case you do not need this attribute. The other method 
  /// is to mark the value type property with this attribute. In this case, the default(T) value identifies the 
  /// NULL value in database column. The system makes the substitution automatically.
  /// Entity ref properties are not nullable by default.
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class NullableAttribute : EntityModelAttributeBase { }

  /// <summary> Marks property as computable from other properties. The computation is performed on the client (c# method), 
  /// database is not involved. </summary>
  /// <remarks>Must refer to a static function accepting a single parameter of the "entity" type. 
  /// For example: 
  /// <code>
  ///   public static string GetFullName(IPerson person) { 
  ///     return person.FirstName + " " + person.LastName; 
  ///   }
  /// </code>
  /// The static function must be defined either in the entity module that defines the entity,
  /// or in a class that is registered using module.RegisterFunctions method. 
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class ComputedAttribute : EntityModelAttributeBase {
    public Type MethodClass;
    public string MethodName;
    public bool Persist; 

    [Obsolete("This constructor is obsolete, use another constructor")]
    public ComputedAttribute(Type methodClass, string methodName) {
      MethodClass = methodClass;
      MethodName = methodName; 
    }

    public ComputedAttribute(string methodName, Type methodClass = null) {
      MethodClass = methodClass;
      MethodName = methodName;
    }
  }

  /// <summary>
  /// Specifies list of members that this member depends on. Might be used with Computed attribute to automatically raise 
  /// property changed event for computed property when any of the properties used in its computatation change.  
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class DependsOnAttribute : EntityModelAttributeBase {
    public string MemberNames;
    public DependsOnAttribute(string memberNames) {
      MemberNames = memberNames;
    }
  }


  public delegate void ValidateMethod<TEntity>(TEntity entity);

  /// <summary> Specifies validation method for an entity. </summary>
  /// <remarks>
  /// <para> The method must have a signature based on generic delegate ValidateMethod: </para>
  /// <code>
  ///   public delegate void ValidateMethod&lt;TEntity&gt;(EntityValidator validator, TEntity entity);
  /// </code>
  /// <para>
  /// The method is invoked after the system-provided default validation. 
  /// Note: c# does not allow specifying method references as attribute parameters. As a workaround, we 
  /// denote method using (Type, Method name) pair.
  /// </para>
  /// </remarks>
  [AttributeUsage(AttributeTargets.Interface)]
  public partial class ValidateAttribute : EntityModelAttributeBase {
    public Type MethodClass;
    public string MethodName;

    public ValidateAttribute(Type methodClass, string methodName) {
      MethodClass = methodClass;
      MethodName = methodName;
    }
  }

  
  /// <summary>
  /// Identifies a property as a "secret" - it can never be read, only written. It is excluded from output lists in all
  /// SELECT statements, but still can be used in WHERE clauses. 
  /// </summary>
  /// <remarks>Use if for passwords and other write-once type of values. </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class SecretAttribute: EntityModelAttributeBase {
  }

  /// <summary>
  /// Specifies that instances are created on the fly and should be discarded from session after transaction commits or aborts.
  /// </summary>
  /// <remarks>Example: transaction log records. The records are generated on-the-fly when updates are submitted and should be discarded if transaction aborts. 
  /// If transaction is aborted and application submits the update again, a new record(s) will be generated. 
  /// </remarks>
  [AttributeUsage(AttributeTargets.Interface)]
  public partial class DiscardOnAbortAttribute: EntityModelAttributeBase {
  }

  /// <summary>Provides a default display format or method for an entity. </summary>
  /// <remarks>
  /// You can provide a Format string with embedded property names enclosed in braces, for ex: "{Title}").
  /// Alternatively you can use a custom formatting method - in this case you must
  /// provide the name and container class and method name pointing to a static method with signature like
  /// <code>
  ///  public static string DisplayBook(IBook book)' {..}
  /// </code>
  /// <para>
  /// Examples: [Display("{Title}")]  
  ///           [Display("{LastName}, {FirstName}")]
  ///           [Display(typeof(BookExtensions), "DisplayBook")] 
  /// </para>
  /// </remarks>
  [AttributeUsage(AttributeTargets.Interface)]
  public partial class DisplayAttribute: EntityModelAttributeBase {
    public string Format;
    public Type MethodClass;
    public string MethodName;
    public DisplayAttribute(string format) {
      Format = format;
    }
    public DisplayAttribute(Type methodClass, string methodName) {
      MethodClass = methodClass;
      MethodName = methodName;
    }
  }

  /// <summary>For a property that is a reference to another entity, sets "ON DELETE CASCADE" option on corresponding constraint in database.</summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class CascadeDeleteAttribute: EntityModelAttributeBase {
  }

  /// <summary>Marks a property/column that should not be modified (change definition, type, etc) 
  /// in DB upgrade process.</summary>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class AsIsAttribute: EntityModelAttributeBase {
  }

  /// <summary>Specifies SQL expression that implements the function SQL.</summary>
  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
  public partial class SqlExpressionAttribute : EntityModelAttributeBase {
    public DbServerType ServerType; 
    public string Expression; 
    public SqlExpressionAttribute(DbServerType serverType, string expression) {
      ServerType = serverType;
      Expression = expression; 
    }
  }

  /// <summary> Marks property as DB-computed: dynamic expression or column with expression. </summary>
  /// <remarks> For Kind=Expression there is no matching column, the value is produced by SQL expression
  /// in SELECT statements. For other kinds there is a column in the database. 
  /// The property must be readonly.
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class DbComputedAttribute : EntityModelAttributeBase {
    public DbComputedKind Kind;

    public DbComputedAttribute(DbComputedKind kind = DbComputedKind.NoColumn) {
      Kind = kind;
    }
  }

  /// <summary>Instructs the system to bypass authorization checks on an entity.</summary>
  [AttributeUsage(AttributeTargets.Interface)]
  public partial class BypassAuthorizationAttribute: EntityModelAttributeBase {
  }

  /// <summary>Marks an entity as not tracked in Transaction log. Explicitly instructs the system to not track 
  /// an entity, even if it belongs to one of the entity areas being tracked.</summary>
  [AttributeUsage(AttributeTargets.Interface)]
  public class DoNotTrack : EntityModelAttributeBase {
  }

  /// <summary>DISABLED: Grants access to referenced entity or some of its properties if the user has access to the entity that holds the reference.</summary>
  /// <remarks>Authorization will be refactored. Keeping the attribute for convenience, to avoid breaking existing entity definitions.</remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public partial class GrantAccessAttribute : EntityModelAttributeBase {
    public string Properties;
    // public AccessType AccessType;
    public object AccessType;
    public GrantAccessAttribute(string properties = null, object accessType = null) {
      Properties = properties;
      AccessType = accessType;
    }
  }


}//namespace
