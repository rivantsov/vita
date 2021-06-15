using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model; 
using Vita.Data.Driver;
using Vita.Entities.Runtime;
using Vita.Data.Sql;
using Vita.Data.Driver.TypeSystem;
using System.Reflection;

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
    public string LogRefName { get; protected set; } //used for recording DBModel change scripts in the database

    public DbModelObjectBase(DbModel dbModel, string schema, DbObjectType objectType, object entityModelObject = null) {
      DbModel = dbModel;
      Schema = LogRefName = schema; 
      ObjectType = objectType; 
      if (DbModel != null && entityModelObject != null)
        DbModel.RegisterDbObject(entityModelObject, this);
    }
  }

  public class DbSchemaInfo : DbModelObjectBase {
    public EntityArea Area;

    public DbSchemaInfo(DbModel dbModel, EntityArea area) : base(dbModel, area.Name, DbObjectType.Schema) {
      Area = area;
    }
    public DbSchemaInfo(DbModel dbModel, string schema) : base(dbModel, schema, DbObjectType.Schema) {
    }
    public override string ToString() {
      return Schema;
    }
  }

  public class DbTableInfo : DbModelObjectBase {
    public string TableName;
    public EntityKind Kind; 
    public IList<DbColumnInfo> Columns = new List<DbColumnInfo>();
    public EntityInfo Entity;
    public IList<DbKeyInfo> Keys = new List<DbKeyInfo>();
    public IList<DbRefConstraintInfo> RefConstraints = new List<DbRefConstraintInfo>();
    public IList<DbKeyColumnInfo> DefaultOrderBy;

    public DbKeyInfo PrimaryKey;
    public string ViewSql;
    public bool IsMaterializedView;
    public EntityRecordReader RecordReader;
    // special column lists
    public IList<DbColumnInfo> InsertColumns;
    public IList<DbColumnInfo> UpdatableColumns;

    public TextSqlFragment SqlFullName;
    public string DefaultSqlAlias; 

    //Used in analyzing changes. In old model, points to the object in new model, and vice versa
    public DbTableInfo Peer;
    public string FullName;

    public DbTableInfo(DbModel model, string schema, string tableName, EntityInfo entity, DbObjectType objectType = DbObjectType.Table)
         : base(model, schema, objectType, entity) {
      TableName = tableName;
      Entity = entity; //might be null
      Kind = EntityKind.Table;
      if (Entity != null)
        Kind = Entity.Kind;
      else if (objectType == DbObjectType.View)
        Kind = EntityKind.View; // when loading from database
      FullName = model.FormatFullName(Schema, tableName);
      base.LogRefName = DbModelHelper.JoinNames(Schema, TableName);
      if (!IsNullTable())
        model.AddTable(this);
      DefaultSqlAlias = DbModelHelper.GetDefaultSqlAlias(this.TableName);
    }
    public override string ToString() {
      return FullName;
    }
    public bool IsNullTable() {
      return Entity?.EntityType == typeof(INullEntity); 
    }
  }


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
    ForeignKey = 1 << 3,
    Identity = 1 << 4,
    RowVersion = 1 << 5,
    IdentityForeignKey = 1 << 6,

    DbComputedExpr = 1 << 7, //not real column but expr in selects
    DbComputedColumn = 1 << 8,

    NoUpdate = 1 << 10,  // columns with auto-values
    NoInsert = 1 << 11,
    UseParamForLongValues = 1 << 12, //string or byte[], Binary data type with size parameter


    IsChanging = 1 << 16, //set in loaded model, signals that in new version it changes 
    Error = 1 << 17, // column info was loaded with error from database
  }
  
  public class DbColumnInfo : DbModelObjectBase {

    public readonly DbTableInfo Table;
    public EntityMemberInfo Member;

    public string ColumnName;
    public string ColumnNameQuoted;
    public SqlFragment SqlColumnNameQuoted; 

    public DbColumnFlags Flags;

    //public DbColumnTypeInfo TypeInfo;
    public DbTypeInfo TypeInfo;
    public DbValueConverter Converter;
    public string DefaultExpression;
    public string DefaultConstraintName;

    public DbComputedAttribute ComputedAttribute; 
    public CustomSqlSnippet SqlSnippet; 

    //Schema update
    //Used in analyzing changes. In old model, points to the object in new model, and vice versa
    public DbColumnInfo Peer;

    public DbColumnInfo(EntityMemberInfo member, DbTableInfo table, string columnName, DbTypeInfo typeInfo)
                          : base(table.DbModel, table.Schema, DbObjectType.Column, member) {
      Member = member;
      Table = table;
      SetName(columnName); 
      TypeInfo = typeInfo;
      Table.Columns.Add(this);
      if (member.Flags.IsSet(EntityMemberFlags.Nullable))
        Flags |= DbColumnFlags.Nullable;
      if (member.AutoValueType == AutoType.Identity)
        Flags |= DbColumnFlags.Identity | DbColumnFlags.NoUpdate | DbColumnFlags.NoInsert;
      if (member.Flags.IsSet(EntityMemberFlags.ForeignKey)) {
        Flags |= DbColumnFlags.ForeignKey;
        if (member.ForeignKeyOwner.ReferenceInfo.ToKey.Entity.Flags.IsSet(EntityFlags.HasIdentity))
          Flags |= DbColumnFlags.IdentityForeignKey;
      }
      if (member.Flags.IsSet(EntityMemberFlags.RowVersion))
        Flags |= DbColumnFlags.RowVersion | DbColumnFlags.NoUpdate | DbColumnFlags.NoInsert;
      if (member.Flags.IsSet(EntityMemberFlags.NoDbInsert))
        Flags |= DbColumnFlags.NoInsert;
      if (member.Flags.IsSet(EntityMemberFlags.NoDbUpdate))
        Flags |= DbColumnFlags.NoUpdate;
      if (member.Flags.IsSet(EntityMemberFlags.DbComputed))
        Flags |= DbColumnFlags.DbComputedExpr;
      if (_sizableTypes.Contains(member.DataType))
        Flags |= DbColumnFlags.UseParamForLongValues;
      if (member.Flags.IsSet(EntityMemberFlags.Secret))
        Flags |= DbColumnFlags.NoUpdate; //updated only thru custom update method
    }
    static Type[] _sizableTypes = new Type[] { typeof(string), typeof(byte[]), typeof(Binary) };


    public void SetName(string columnName) {
      ColumnName = columnName;
      ColumnNameQuoted = Table.DbModel.Driver.SqlDialect.QuoteName(columnName);
      base.LogRefName = DbModelHelper.JoinNames(Schema, Table.TableName, columnName);
      SqlColumnNameQuoted = new TextSqlFragment(ColumnNameQuoted);
    }

    //constructor loader from the database
    public DbColumnInfo(DbTableInfo table, string columnName, DbTypeInfo typeInfo, bool isNullable)
                            : base(table.DbModel, table.Schema, DbObjectType.Column, null) {
      Table = table;
      SetName(columnName);
      // TO FIX
      TypeInfo = typeInfo;
      if (isNullable)
        this.Flags |= DbColumnFlags.Nullable; 
      Table.Columns.Add(this);
      base.LogRefName = DbModelHelper.JoinNames(table.Schema, table.TableName, columnName);
    }

    public override string ToString() {
      return Table.TableName + "." + ColumnName + ":" + TypeInfo;
    }

  }//class

  public class DbTableFilter {
    public EntityFilter EntityFilter;
    public List<DbColumnInfo> Columns = new List<DbColumnInfo>();
    // SQL with columns without table/alias prefix
    public string DefaultSql;
  }

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
    public DbTableFilter Filter;
    public EntityKeyInfo EntityKey;
    //used only when loading schema from database and finding a foreign key with target not PK, but a random set of fields
    public bool NotSupported;

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
      base.LogRefName = DbModelHelper.JoinNames(table.Schema, name);
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
      base.LogRefName = DbModelHelper.JoinNames(fromKey.Table.Schema, fromKey.Name);
      FromKey.Table.RefConstraints.Add(this); 
    }

    public override string ToString() {
      return FromKey.Name;
    }
  }

  public class DbSequenceInfo : DbModelObjectBase {
    public string Name;
    public string DbTypeName;
    public long StartValue;
    public int Increment;
    public string FullName;
    public SequenceDefinition Definition;
    internal DbSequenceInfo Peer; 

    public DbSequenceInfo(DbModel model, string name, string schema, string dbTypeName, long startValue, int increment) 
                 : base(model, schema, DbObjectType.Sequence, null) {
      Name = name;
      DbTypeName = dbTypeName; 
      StartValue = startValue;
      Increment = increment;
      FullName = model.FormatFullName(schema, name); 
    }

    public DbSequenceInfo(DbModel model, SequenceDefinition definition) 
          : base(model, definition.Module.Area.Name, DbObjectType.Sequence, definition) {
      Name = definition.Name;
      var stype = model.Driver.TypeRegistry.GetDbTypeDef(definition.DataType);
      DbTypeName = stype.Name;
      StartValue = definition.StartValue;
      Increment = definition.Increment;
      Definition = definition;
      FullName = model.FormatFullName(Schema, Name);
    }

  }//class

  public enum DbCustomTypeKind {
    Regular,
    TableType
  }
  public class DbCustomTypeInfo : DbModelObjectBase {
    public DbCustomTypeKind Kind; 
    public string Name;
    public string FullName;
    public bool IsNullable;
    public int Size; 
    public DbCustomTypeInfo Peer; 

    public DbCustomTypeInfo(DbModel model, string schema, string name, DbCustomTypeKind kind, bool isNullable, int size)
                             : base(model, schema, DbObjectType.UserDefinedType) {
      Name = name;
      Kind = kind;
      IsNullable = isNullable;
      Size = size; 
      FullName = model.FormatFullName(Schema, Name);
      model.AddCustomType(this); 
    }
  }

  public class CustomSqlSnippet  {
    public MemberInfo Owner;
    public Type DataType; 
    public DbServerType ServerType;
    public StringTemplate ParsedSql; 
    public SqlTemplate SqlTemplate;
    public int[] ParamsReorder; 

    public CustomSqlSnippet(MemberInfo owner, DbServerType serverType, StringTemplate parsedSql,
           SqlTemplate sqlTemplate, int[] paramsReorder) {
      Owner = owner;
      ServerType = serverType;
      ParsedSql = parsedSql;
      SqlTemplate = sqlTemplate;
      ParamsReorder = paramsReorder;
      DataType = owner.GetMemberReturnType(); 
    }
    public override string ToString() => SqlTemplate.Template;
  }

  public static class DbModelExtensions {
    public static bool IsSet(this DbColumnFlags flags, DbColumnFlags flag) {
      return (flags & flag) != 0;
    }

    public static DbColumnInfo GetColumnByMemberName(this DbTableInfo table, string memberName) {
      var col = table.Columns.FirstOrDefault(c => c.Member.MemberName.Equals(memberName, StringComparison.OrdinalIgnoreCase));
      Util.Check(col != null, "Column for member {0} not found in table {1}.", memberName, table.TableName);
      return col;
    }

    public static DbColumnInfo GetColumn(this DbTableInfo table, string columnName) {
      var col = table.Columns.FirstOrDefault(c => c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
      Util.Check(col != null, "Column {0} not found in table {1}.", columnName, table.TableName);
      return col; 
    }
  }

}
