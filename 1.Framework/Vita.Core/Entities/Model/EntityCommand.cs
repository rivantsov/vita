using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

using Vita.Common;
using Vita.Entities.Authorization;

namespace Vita.Entities.Model {

  public enum PagingMode {
    Client = 0, //default
    DataStore,
  }

  [Flags]
  public enum EntityCommandFlags {
    None = 0,
    IsQuery = 1 << 2,
    IsCustom = 1 << 3,
  }

  public enum EntityCommandKind {
    //Default CRUD types
    SelectAll,
    SelectAllPaged,
    SelectByKey,
    SelectByKeyArray,
    SelectByKeyManyToMany,
    Update,
    Insert,
    Delete,

    //Query-based custom commands
    CustomSelect,
    CustomUpdate,
    CustomInsert,
    CustomDelete,
    // Partial update
    PartialUpdate,
  }


  public class EntityCommand : HashedObject {
    public string CommandName;
    public string Description;
    public EntityArea Area { get; internal set; } // defaulted to TargetEntityInfo.Area
    public EntityCommandKind Kind { get; private set; }
    public Type TargetEntityType { get; private set; }
    public EntityInfo TargetEntityInfo { get; internal set; }

    public List<EntityCommandParameter> Parameters = new List<EntityCommandParameter>();
    public EntityCommandFlags Flags; 
    
    // Fields used by certain command kinds -------------------------------
    public List<string> UpdateMemberNames;//For PartialUpdate - names of members to update
    public EntityKeyInfo SelectKey;       //Key for SelectByKey command
    //Secondary key for SelectByKeyManyToMany command; (IBookAuthor.Author key, for GetBookAuthors)
    public EntityKeyInfo SelectKeySecondary; 
    public Authorization.AccessType AccessType;
    public string Filter;

    public EntityCommand(string commandName, string description, EntityCommandKind kind, Type entityType, EntityArea area) :
                        this(commandName, description, kind) {
      TargetEntityType = entityType;
      Area = area;
    }

    public EntityCommand(string commandName, string description, EntityCommandKind kind, EntityInfo entity, EntityKeyInfo selectKey = null) 
                 : this(commandName, description, kind) {
      TargetEntityInfo = entity;
      Area = entity.Area;
      TargetEntityType = entity.EntityType;
      SelectKey = selectKey;
    }

    private EntityCommand(string commandName, string description, EntityCommandKind kind) {
      CommandName = commandName;
      Description = description;
      Kind = kind;
      AccessType = Kind.GetAuthorizationAccessType();
      switch (Kind) {
        case EntityCommandKind.SelectAll: case EntityCommandKind.SelectAllPaged: 
        case EntityCommandKind.SelectByKey:   case EntityCommandKind.SelectByKeyManyToMany:  
        case EntityCommandKind.SelectByKeyArray:
          Flags |= EntityCommandFlags.IsQuery;
          break; 
        case EntityCommandKind.CustomSelect:
          Flags |= EntityCommandFlags.IsQuery | EntityCommandFlags.IsCustom;
          break; 
        case EntityCommandKind.CustomInsert: case EntityCommandKind.CustomUpdate: case EntityCommandKind.CustomDelete:
          Flags |= EntityCommandFlags.IsCustom;
          break; 
      }
    }

    public override string ToString() {
      return CommandName;
    }

  }//class


  public class EntityCommandParameter {
    public string Name;
    public Type DataType;
    public int Size;
    public object DefaultValue;
    public EntityMemberInfo SourceMember; //For CRUD commands identifies the source of the parameter value.
    public DbType? ExplicitDbType = null;
    public byte Scale;
    public byte Precision;

    public bool IsLinqConst; // true for Linq literal constants
    public ParameterDirection Direction = ParameterDirection.Input; //true for Identity columns

    public EntityCommandParameter(EntityMemberInfo sourceMember) {
      SourceMember = sourceMember;
      Name = sourceMember.MemberName; //@ is added by DbSqlBuilder
      DataType = sourceMember.DataType;
      Size = sourceMember.Size;
      DefaultValue = sourceMember.DefaultValue;
    }

    public EntityCommandParameter(string name, Type dataType, int size, object defaultValue) {
      Name = name;
      DataType = dataType;
      Size = size;
      if (DataType == typeof(string))
        DefaultValue = "_";
      else if (DataType == typeof(byte[]))
        DefaultValue = new byte[] { 0 };
      else
        DefaultValue = defaultValue;
    }

    public override string ToString() {
      return Name + "/" + DataType;
    }
    public override int GetHashCode() {
      return Name.GetHashCode();
    }
  }

}//ns
