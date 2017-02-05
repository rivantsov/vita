using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Model; 
using Vita.Data.Driver;
using Vita.Data.Linq;

namespace Vita.Data.Model {

  public enum DbObjectType {
    Schema,
    Table,
    Column,
    Key,
    RefConstraint,
    View,
    Command,
    Sequence,
    UserDefinedType,
    Other = 100,
  }


  // Base for all DB model objects. Constructor makes automatic registration of the object in DbModel's dictionary. 
  // This dictionary is used to lookup DbObject by its entity model counterpart.
  public abstract class DbModelObjectBase {
    public DbModel DbModel;
    public string Schema;
    public DbObjectType ObjectType;
    public string GlobalName { get; protected set; } //used for recording DBModel change scripts in the database

    public DbModelObjectBase(DbModel dbModel, string schema, DbObjectType objectType, HashedObject entityModelObject = null) {
      DbModel = dbModel;
      Schema = GlobalName = schema; 
      ObjectType = objectType; 
      if (DbModel != null && entityModelObject != null)
        DbModel.RegisterDbObject(entityModelObject, this);
    }
  }

  public class DbSchemaInfo : DbModelObjectBase {
    public EntityArea Area;

    public DbSchemaInfo(DbModel model, EntityArea area, string schema)  : base(model, schema, DbObjectType.Schema, area) {
      Area = area;
      base.GlobalName = Schema; 
    }
    public DbSchemaInfo(DbModel model, string schema) : base(model, schema, DbObjectType.Schema, null) {
      base.GlobalName = Schema;
    }
    public override string ToString() {
      return Schema;
    }
  }

  public class DbTableInfo : DbModelObjectBase {
    public string TableName;
    public readonly string FullName;
    public EntityKind Kind; 
    public IList<DbColumnInfo> Columns = new List<DbColumnInfo>();
    public EntityInfo Entity;
    public IList<DbKeyInfo> Keys = new List<DbKeyInfo>();
    public IList<DbRefConstraintInfo> RefConstraints = new List<DbRefConstraintInfo>();
    public IList<DbKeyColumnInfo> DefaultOrderBy;

    public DbKeyInfo PrimaryKey;
    public List<DbCommandInfo> CrudCommands = new List<DbCommandInfo>();
    //Used in analyzing changes. In old model, points to the object in new model, and vice versa
    public DbTableInfo Peer;
    public string ViewSql;
    public string ViewHash; 
    public bool IsMaterializedView;
    public EntityMaterializer EntityMaterializer;

    public DbTableInfo(DbModel model, string schema, string tableName, EntityInfo entity, DbObjectType objectType = DbObjectType.Table)
         : base(model, schema, objectType, entity) {
      TableName = tableName;
      Entity = entity; //might be null
      Kind = EntityKind.Table;
      if (Entity != null)
        Kind = Entity.Kind;
      else if (objectType == DbObjectType.View)
        Kind = EntityKind.View; // when loading from database
      FullName = model.Driver.FormatFullName(Schema, tableName);
      base.GlobalName = DbModelHelper.GetGlobalName(Schema, TableName);
      model.AddTable(this); 
    }
    public override string ToString() {
      return FullName;
    }
    public override int GetHashCode() {
      return FullName.GetHashCode();
    }
  }

  public class DbTypeInfo {
    static Func<object, object> _noConv = x => x; 

    public VendorDbTypeInfo VendorDbType;
    public DbType DbType; // most often the same as VendorDbType.DbType; SQLite driver assigns specific values here
    public string SqlTypeSpec;
    public bool IsNullable;
    public long Size;
    public byte Precision;
    public byte Scale;
    public string InitExpression; //value to initialize nullable column when it switches to non-nullable; this string will be put directly into update SQL
    //Value converters
    public Func<object, object> ColumnToPropertyConverter = _noConv;
    public Func<object, object> PropertyToColumnConverter = _noConv;

    public DbTypeInfo(VendorDbTypeInfo vendorTypeInfo, string sqlTypeSpec, bool isNullable,
                         long size, byte precision, byte scale, string initExpression = null) {
      VendorDbType = vendorTypeInfo;
      DbType = VendorDbType.DbType; 
      SqlTypeSpec = sqlTypeSpec;
      IsNullable = isNullable;
      Size = size;
      Precision = precision;
      Scale = scale;
      InitExpression = initExpression;
    }

    public string ToLiteral(object value) {
      return VendorDbType.ValueToLiteral(this, value);
    }

    public override string ToString() {
      return SqlTypeSpec;
    }

  }//class


  // Column reference includes Asc/Desc flag, used in keys (indexes)
  public class DbKeyColumnInfo {
    public readonly DbColumnInfo Column;
    public readonly EntityKeyMemberInfo EntityKeyMember; //might be null for model loaded from the database
    public bool Desc;

    public DbKeyColumnInfo(DbColumnInfo column, EntityKeyMemberInfo keyMember = null, bool desc = false) {
      Column = column;
      EntityKeyMember = keyMember;
      if(EntityKeyMember == null)
        Desc = desc; 
      else
        Desc = EntityKeyMember.Desc;
    }

    public override string ToString() {
      var s = Column.ColumnName;
      if(Desc)
        s += " DESC";
      return s; 
    }
  }
  

  [Flags]
  public enum DbColumnFlags {
    None = 0,
    Nullable = 1,
    PrimaryKey = 1 << 1,
    ClusteredIndex = 1 << 2,

    Identity = 1 << 4,
    NoUpdate = 1 << 5,  // columns with auto-values
    NoInsert = 1 << 6,

    ForeignKey = 1 << 8,
    IdentityForeignKey = 1 << 9,

    Error = 1 << 16, // column info was loaded with error from database
  }
  
  public class DbColumnInfo : DbModelObjectBase {

    public readonly DbTableInfo Table;
    public EntityMemberInfo Member;
    
    public string ColumnName;
    public DbColumnFlags Flags;

    public DbTypeInfo TypeInfo;
    public string DefaultExpression;
    public string DefaultConstraintName;

    //Schema update
    //Used in analyzing changes. In old model, points to the object in new model, and vice versa
    public DbColumnInfo Peer;

    public DbColumnInfo(EntityMemberInfo member, DbTableInfo table, string columnName, DbTypeInfo typeInfo)
                          : base(table.DbModel, table.Schema, DbObjectType.Column, member) {
      Member = member;
      Table = table;
      ColumnName = columnName;
      TypeInfo = typeInfo;
      Table.Columns.Add(this);
      base.GlobalName = DbModelHelper.GetGlobalName(Schema, table.TableName, columnName); 
      if (member.Flags.IsSet(EntityMemberFlags.Nullable))
        Flags |= DbColumnFlags.Nullable;
      if (member.Flags.IsSet(EntityMemberFlags.Identity))
        Flags |= DbColumnFlags.Identity;
      if (member.Flags.IsSet(EntityMemberFlags.ForeignKey)) {
        Flags |= DbColumnFlags.ForeignKey;
        if (member.ForeignKeyOwner.ReferenceInfo.ToKey.Entity.Flags.IsSet(EntityFlags.HasIdentity))
          Flags |= DbColumnFlags.IdentityForeignKey;
      }
    }
    //constructor loader from the database
    public DbColumnInfo(DbTableInfo table, string columnName, DbTypeInfo typeInfo)
      : base(table.DbModel, table.Schema, DbObjectType.Column, null) {
      Table = table;
      ColumnName = columnName;
      TypeInfo = typeInfo;
      if (typeInfo.IsNullable)
        this.Flags |= DbColumnFlags.Nullable; 
      Table.Columns.Add(this);
      base.GlobalName = DbModelHelper.GetGlobalName(table.Schema, table.TableName, columnName);
    }

    public override string ToString() {
      return Table.TableName + "." + ColumnName + ":" + TypeInfo;
    }

  }//class

  // DbKeyInfo is a generalized concept for all database objects based on group of columns: 
  // indexes, primary or foreign keys. Note that we use the term "foreign key" in a narrow sense, 
  // to refer ONLY to the set of columns in child table; the "relation" between foreign key and primary/unique
  // key in another table is presented by DbRefConstraintInfo class.
  public class DbKeyInfo : DbModelObjectBase {
    public string Name;
    public DbTableInfo Table;
    public KeyType KeyType;
    public List<DbKeyColumnInfo> KeyColumns = new List<DbKeyColumnInfo>();
    public List<DbColumnInfo> IncludeColumns = new List<DbColumnInfo>();
    public string Filter;
    public EntityKeyInfo EntityKey;
    //used only when loading schema from database and finding a foreign key with target not PK, but a random set of fields
    public bool NotSupported;
    // set from [OrderBy] attribute on list property in parent/target entity
    public IList<DbKeyColumnInfo> OrderByForSelect;

    //Used in analyzing changes. In old model, points to the object in new model, and vice versa
    public DbKeyInfo Peer;

    public override string ToString() {
      return Name + "(" + KeyType + ")";
    }
    public DbKeyInfo(string name, DbTableInfo table, KeyType type, EntityKeyInfo entityKey = null) 
                        : base(table.DbModel, table.Schema, DbObjectType.Key, entityKey) {
      Name = name;
      Table = table;
      KeyType = type; 
      EntityKey = entityKey;
      table.Keys.Add(this);
      base.GlobalName = DbModelHelper.GetGlobalName(table.Schema, name);
    }
  }//class

  public class DbRefConstraintInfo : DbModelObjectBase {
    public DbKeyInfo FromKey;
    public DbKeyInfo ToKey;
    public EntityReferenceInfo OwnerReference; 
    public bool CascadeDelete;

    //Used in analyzing changes. In old model, points to the object in new model, and vice versa
    public DbRefConstraintInfo Peer;

    public DbRefConstraintInfo(DbModel dbModel, DbKeyInfo fromKey, DbKeyInfo toKey, bool cascadeDelete, EntityReferenceInfo ownerRef = null) 
             : base(dbModel, fromKey.Schema, DbObjectType.RefConstraint, ownerRef) {
      Util.Check(fromKey != null, "fromKey may not be null.");
      Util.Check(toKey != null, "toKey may not be null.");
      FromKey = fromKey;
      ToKey = toKey;
      OwnerReference = ownerRef;
      CascadeDelete = cascadeDelete;
      base.GlobalName = DbModelHelper.GetGlobalName(fromKey.Table.Schema, fromKey.Name);
    }

    public override string ToString() {
      return FromKey.Name;
    }
  }

  public class DbParamInfo {
    public EntityCommandParameter Owner; 
    public string Name;
    public DbTypeInfo TypeInfo;
    public ParameterDirection Direction = ParameterDirection.Input;
    public DbColumnInfo SourceColumn; //might be null
    // for parameters of commands created from LINQ queries; in fact, parameter is a literal constant 
    public object DefaultValue;
    //Index of value in argument array of the command call; 
    public int ArgIndex;

    public Func<object, string> ToLiteralConverter; //converter for template

    public DbParamInfo(EntityCommandParameter owner, string name, DbColumnInfo sourceColumn, int argIndex) 
                        : this(owner, name, argIndex) {
      SourceColumn = sourceColumn;
      Direction = owner.Direction;
      TypeInfo = sourceColumn.TypeInfo;
    }

    public DbParamInfo(EntityCommandParameter owner, string name, DbTypeInfo typeInfo, int argIndex) : this(owner, name, argIndex) {
      TypeInfo = typeInfo;
      Direction = ParameterDirection.Input;
    }

    public DbParamInfo(EntityCommandParameter owner, string name, int argIndex) {
      Owner = owner;
      Name = name;
      ArgIndex = argIndex;
      DefaultValue = owner.DefaultValue; 
    }

    public override string ToString() {
      return this.Name + "/" + TypeInfo; 
    }
  }//class

  [Flags]
  public enum DbCommandFlags {
    None = 0,
    AutoGenerated = 1 << 1, // Set for commands from procs found in the database that are generated by VITA
  }

  public class DbCommandInfo : DbModelObjectBase {
    public string CommandName { get; private set; }
    public string Description;
    public DbTableInfo Table;
    public string Sql;
    public EntityCommandKind Kind;
    public DbExecutionType ExecutionType;
    public List<DbParamInfo> Parameters = new List<DbParamInfo>();
    public List<DbParamInfo> OutputParameters = new List<DbParamInfo>();
    public DbParamInfo ReturnValueParameter;

    public Vita.Entities.Runtime.EntityMaterializer EntityMaterializer; //for query commands

    public DbCommandFlags Flags;
    public string DescriptiveTag; //saved in comment line in stored proc body, used to identify procedure and relation to table

    //stored proc 
    public string StoredProcText;
    public string StoredProcBody;
    public string SourceHash;
    public string FullCommandName;
    public CommandType CommandType = CommandType.Text; 
    //vendor-specific info loaded from database. For Postgres we use it to store list of arg types - they are needed for 'DROP Function' call
    public object CustomTag;

    //for partial selects/updates - fields to be selected/updated
    //public FieldMask FieldMask;
    public DbCommandInfo Peer;
    public EntityCommand EntityCommand;
    // If true, SQL is a template that must have parameters inserted as literals
    public bool IsTemplatedSql;

    public List<Action<DataConnection, IDbCommand, EntityRecord>> PostUpdateActions = new List<Action<DataConnection, IDbCommand, EntityRecord>>(); 

    public DbCommandInfo(EntityCommand entityCommand, string commandName, DbTableInfo table, DbExecutionType executionType, string sql, string tag)
                            : base(table.DbModel, table.Schema, DbObjectType.Command, entityCommand) {
      EntityCommand = entityCommand;
      CommandName = commandName;
      Table = table;
      ExecutionType = executionType;
      Sql = sql;
      Description = EntityCommand.Description;
      DescriptiveTag = tag;
      //derived entities
      FullCommandName = Table.DbModel.Driver.FormatFullName(Schema, CommandName);
      Kind = entityCommand.Kind;
      var dbModel = table.DbModel; 
      dbModel.AddCommand(this);
      if(Table != null)
        Table.CrudCommands.Add(this);
      base.GlobalName = DbModelHelper.GetGlobalName(Schema, commandName);
    }

    //Creates a stub for commands loaded from the database
    public DbCommandInfo(DbModel model, string schema, string commandName, string text, string tag) : 
          base(model, schema, DbObjectType.Command, null) {
      CommandName = commandName;
      StoredProcText = text;
      DescriptiveTag = tag;
      FullCommandName = model.Driver.FormatFullName(Schema, CommandName);
      model.AddCommand(this);
      base.GlobalName = DbModelHelper.GetGlobalName(Schema, commandName);
    }


    public DbParamInfo AddParameter(EntityCommandParameter owner, string name, DbColumnInfo column, int argIndex) {
      return AddParameter(new DbParamInfo(owner, name, column, argIndex));
    }

    public DbParamInfo AddParameter(EntityCommandParameter owner, string name, DbTypeInfo typeInfo, int argIndex) {
      return AddParameter(new DbParamInfo(owner, name, typeInfo, argIndex));
    }

    private DbParamInfo AddParameter(DbParamInfo parameter) {
      Parameters.Add(parameter);
      switch (parameter.Direction) {
        case ParameterDirection.Output:
        case ParameterDirection.InputOutput:
          OutputParameters.Add(parameter);
          break;
        case ParameterDirection.ReturnValue:
          ReturnValueParameter = parameter;
          break;
      }
      return parameter;
    }

    public override string ToString() {
      return FullCommandName;
    }

    public bool StoredProcDefined() {
      return !string.IsNullOrEmpty(StoredProcText);
    }
  }//class

  public class DbSequenceInfo : DbModelObjectBase {
    public string Name;
    public DbTypeInfo DbType;
    public long StartValue;
    public int Increment;
    public string FullName;
    public SequenceDefinition Definition;
    public DbCommandInfo GetNextValueCommand;
    internal DbSequenceInfo Peer; 

    public DbSequenceInfo(DbModel model, string name, string schema, DbTypeInfo dbType, long startValue, int increment) 
                 : base(model, schema, DbObjectType.Sequence, null) {
      Name = name;
      DbType = dbType;
      StartValue = startValue;
      Increment = increment;
      FullName = model.Driver.FormatFullName(schema, name); 
    }

    public DbSequenceInfo(DbModel model, SequenceDefinition definition) 
          : base(model, GetSchema(model, definition), DbObjectType.Sequence, definition) {
      Name = definition.Name;
      DbType = model.Driver.TypeRegistry.GetDbTypeInfo(definition.DataType, 0);
      StartValue = definition.StartValue;
      Increment = definition.Increment;
      Definition = definition;
      FullName = model.Driver.FormatFullName(Schema, Name);
    }

    private static string GetSchema(DbModel model, SequenceDefinition sequence) {
      return sequence.ExplicitSchema ?? model.Config.GetSchema(sequence.Module.Area); 
    }
  }//class

  public enum DbCustomTypeKind {
    Unknown,
    ArrayAsTable
  }
  public class DbCustomTypeInfo : DbModelObjectBase {
    public DbCustomTypeKind Kind; 
    public string Name;
    public string FullName;
    public DbCustomTypeInfo Peer; 

    public DbCustomTypeInfo(DbModel model, string schema, string name, DbCustomTypeKind kind = DbCustomTypeKind.Unknown)
      : base(model, schema, DbObjectType.UserDefinedType) {
      Name = name;
      Kind = kind;
      FullName = model.Driver.FormatFullName(Schema, Name);
    }
  }

  public static class DbModelExtensions {
    public static bool IsSet(this DbColumnFlags flags, DbColumnFlags flag) {
      return (flags & flag) != 0;
    }

    public static bool IsSet(this DbCommandFlags flags, DbCommandFlags flag) {
      return (flags & flag) != 0;
    }
    public static DbColumnInfo GetColumnByMemberName(this DbTableInfo table, string memberName) {
      return table.Columns.FirstOrDefault(c => c.Member.MemberName == memberName);
    }
  }

}
