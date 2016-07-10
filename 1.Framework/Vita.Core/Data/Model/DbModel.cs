using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Data.Linq;

namespace Vita.Data.Model {

  //Physical model
  public class DbModel {

    public EntityApp EntityApp;
    public DbModelConfig Config;
    public List<DbSchemaInfo> Schemas = new List<DbSchemaInfo>();
    public DbVersionInfo VersionInfo; 

    internal bool CustomCommandsCompiled { get; private set; }

    // Contains all DbObjects (tables, keys, commands) by name. 
    Dictionary<string, DbModelObjectBase> _dbObjectsByName = new Dictionary<string, DbModelObjectBase>(StringComparer.InvariantCultureIgnoreCase);
    Dictionary<string, DbCommandInfo> _commandsByTag = new Dictionary<string, DbCommandInfo>(StringComparer.InvariantCultureIgnoreCase);
    IList<DbTableInfo> _tables = new List<DbTableInfo>();
    IList<DbCommandInfo> _commands = new List<DbCommandInfo>();
    IList<DbSequenceInfo> _sequences = new List<DbSequenceInfo>();
    IList<DbCustomTypeInfo> _customTypes = new List<DbCustomTypeInfo>(); 

    // Cache - we use ObjectCache for compiled queries; we don't need cache entries to expire, but we want to limit cache size automatically
    // ObjectCache provides this cleanup on overflow functionality
    public readonly ObjectCache<TranslatedLinqCommand> QueryCache = new ObjectCache<TranslatedLinqCommand>("LinqQueryCache", expirationSecs: 60 * 5);

    //Table of all Db objects; accessed by entity-model object as key 
    private Dictionary<HashedObject, DbModelObjectBase> _allObjects = new Dictionary<HashedObject, DbModelObjectBase>();

    public ICollection<DbTableInfo> Tables {
      get {return _tables;}
    }
    public ICollection<DbCommandInfo> Commands {
      get { return _commands; }
    }
    public ICollection<DbSequenceInfo> Sequences {
      get { return _sequences; }
    }
    public ICollection<DbCustomTypeInfo> CustomDbTypes {
      get { return _customTypes; }
    }
    public DbDriver Driver { get { return Config.Driver; } }

    public LinqSqlProvider LinqSqlProvider {
      get {
        if(_linqSqlProvider == null)
          _linqSqlProvider = this.Driver.CreateLinqSqlProvider(this);
        return _linqSqlProvider;
      }
    } LinqSqlProvider _linqSqlProvider;

    /// <summary>Constructs DbModel from EntityModel.</summary>
    /// <param name="entityApp"></param>
    /// <param name="config"></param>
    internal DbModel(EntityApp entityApp, DbModelConfig config) {
      Util.Check(entityApp != null, "entityApp parameter may not be null.");
      EntityApp = entityApp;
      Config = config;
      //Add schemas 
      foreach(var area in entityApp.Areas) {
        Schemas.Add(new DbSchemaInfo(this, area, config.GetSchema(area)));
      }
      VersionInfo = new DbVersionInfo(EntityApp, Config);
    }

    /// <summary>Constructs an empty DbModel. This constructor is used for models loaded from database. </summary>
    /// <param name="config">DB model config object.</param>
    internal DbModel(DbModelConfig config) {
      Config = config;
    }

    #region Construction-time methods
    public void AddTable(DbTableInfo table) {
      Util.Check(!_dbObjectsByName.ContainsKey(table.FullName), "Duplicate table in DbModel: {0}", table.FullName);
      _tables.Add(table);
      _dbObjectsByName[table.FullName] = table; 
    }
    public void AddCommand(DbCommandInfo command) {
      Util.Check(!_dbObjectsByName.ContainsKey(command.FullCommandName), "Duplicate proc in DbModel: {0}", command.FullCommandName);
      if (command == null) return; 
      _commands.Add(command);
      _dbObjectsByName[command.FullCommandName] = command;
      if (!string.IsNullOrWhiteSpace(command.DescriptiveTag))
        _commandsByTag[command.DescriptiveTag] = command; 
    }
    public void AddSequence(DbSequenceInfo sequence) {
      Util.Check(!_dbObjectsByName.ContainsKey(sequence.FullName), "Duplicate sequence in DbModel: {0}", sequence.FullName);
      _sequences.Add(sequence);
      _dbObjectsByName[sequence.FullName] = sequence; 
    }

    public void AddCustomType(DbCustomTypeInfo type) {
      _customTypes.Add(type);
      _dbObjectsByName[type.FullName] = type;
    }

    public void RegisterDbObject(HashedObject entityModelObject, DbModelObjectBase dbObject) {
      if (entityModelObject == null) return;
      if (dbObject.ObjectType == DbObjectType.Column)
        return; // do not register columns, there are too many of them, and columns are not real standalone objects
      _allObjects[entityModelObject] = dbObject; 
    }//method
    #endregion

    public T LookupDbObject<T>(HashedObject key, bool throwNotFound = false) where T : DbModelObjectBase {
      DbModelObjectBase result;
      _allObjects.TryGetValue(key, out result);
      if(result == null && throwNotFound)
        Util.Throw("Failed to lookup DbObject for entity model object {0}", key);
      return (T)result;
    }
    public DbModelObjectBase GetDbObject(string fullName) {
      DbModelObjectBase obj;
      _dbObjectsByName.TryGetValue(fullName, out obj);
      return obj;
    }
    public DbCommandInfo GetCommand(string fullName) {
      return GetDbObject(fullName) as DbCommandInfo;
    }
    public DbCommandInfo GetCommandByTag(string tag) {
      DbCommandInfo cmd; 
      _commandsByTag.TryGetValue(tag, out cmd);
      return cmd; 
    }
    
    public DbTableInfo GetTable(string fullName) {
      return GetDbObject(fullName) as DbTableInfo;
    }
    public DbTableInfo GetTable(string schemaName, string tableName) {
      var fullName = Config.Driver.GetFullName(schemaName, tableName);
      return GetTable(fullName);
    }
    
    public DbTableInfo GetTable(Type entityType, bool throwIfNotFound = true) {
      var entInfo = this.EntityApp.Model.GetEntityInfo(entityType, throwIfNotFound);
      if (entInfo == null)
        return null; 
      var tableInfo = this.LookupDbObject<DbTableInfo>(entInfo, throwIfNotFound);
      return tableInfo;
    }

    public DbKeyInfo FindKey(string name) {
      foreach (var t in this.Tables)
        foreach (var key in t.Keys)
          if (string.Compare(key.Name, name, true) == 0) return key;
      return null;
    }

    public bool ContainsSchema(string schema) {
      var supportsSchemas = Config.Driver.Supports(DbFeatures.Schemas);
      if (!supportsSchemas && string.IsNullOrEmpty(schema)) 
        return true; // we consider NULL schema is OK 
      var sch = Schemas.FirstOrDefault(s => s.Schema == schema);
      return sch != null; 
    }

    public const string ViewHashKeyPrefix = "ViewHash-";
    public static string GetViewKey(Vita.Data.Model.DbTableInfo view) {
      return ViewHashKeyPrefix + view.Schema + "." + view.TableName;
    }


    internal void ResetPeerRefs() {
      foreach (var t in this.Tables) {
        t.Peer = null;
        foreach (var c in t.Columns)
          c.Peer = null;
        foreach (var k in t.Keys)
          k.Peer = null;
        foreach (var rc in t.RefConstraints)
          rc.Peer = null;
        foreach (var cmd in t.CrudCommands)
          cmd.Peer = null;
      }
      foreach (var seq in this.Sequences)
        seq.Peer = null;
      foreach(var tp in this.CustomDbTypes)
        tp.Peer = null; 
    }




  }//class

}
