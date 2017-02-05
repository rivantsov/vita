using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Model;

namespace Vita.Data.Model {

  /// <summary> Enumerates objects with names. </summary>
  public enum DbNamedObjectType {
    Column,
    Table,
    Procedure,
    Index,
    Key,
  }

  /// <summary>A class providing default methods for constructing database object names. </summary>
  /// <remarks>Override methods in this class to customize the naming behavior.</remarks>
  public class DbNamingPolicy {
    public string ViewPrefix = "v";
    public string TablePrefix = string.Empty; 

    /// <summary> Called by the system to let application adjust the name of a database object - table, column, key, etc.</summary>
    /// <param name="objectType">The object type.</param>
    /// <param name="name">The automatically generated name.</param>
    /// <param name="objectInfo">The metadata info for the object.</param>
    /// <returns>Adjusted name.</returns>
    public virtual string CheckName(DbNamedObjectType objectType, string name, object objectInfo) {
      return name;
    }//method

    /// <summary>Constructs the default name for a stored procedure.</summary>
    /// <param name="command">Entity command.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="operation">Operation; usually a type of a CRUD command.</param>
    /// <param name="suffix">Exra suffix, usually one or more columns involved in the operation.</param>
    /// <returns>The procedure name.</returns>
    /// <remarks>Constructs the name of db command (stored procedure) by combining Table name, operation and optionally
    /// the list of columns involved. For ex: ProductSelectByName.</remarks>
    public virtual string GetDbCommandName(EntityCommand command, string tableName, string operation, string suffix = null) {
      return command.CommandName;
    }

    public virtual string GetDbParameterName(string baseName) {
      return baseName; 
    }

    public virtual string GetDbTableViewName(EntityInfo entity, DbModelConfig config) {
      var name = entity.TableName;
      if(!string.IsNullOrWhiteSpace(name))
        return name;
      switch(entity.Kind) {
        case EntityKind.View:
          name = this.ViewPrefix + entity.Name;
          break; 
        case EntityKind.Table:
        default:
          name = this.TablePrefix + entity.Name;
          break; 
      }
      if(config.Options.IsSet(DbOptions.AddSchemaToTableNames))
        name = entity.Area.Name + "_" + name;
      return name; 
    }//method
  }//class



}
