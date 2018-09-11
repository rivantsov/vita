using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.SqlGen;

namespace Vita.Data.Model {

  //Physical model
  public class DbModel {

    public EntityApp EntityApp;
    public DbModelConfig Config;
    public List<DbSchemaInfo> Schemas = new List<DbSchemaInfo>();
    public DbVersionInfo VersionInfo;
    public EntityModel EntityModel => EntityApp.Model; 

    internal bool CustomCommandsCompiled { get; private set; }

    IList<DbTableInfo> _tables = new List<DbTableInfo>();
    IDictionary<string, DbTableInfo> _tablesByName = new Dictionary<string, DbTableInfo>(StringComparer.InvariantCultureIgnoreCase);
    IList<DbSequenceInfo> _sequences = new List<DbSequenceInfo>();
    IList<DbCustomTypeInfo> _customTypes = new List<DbCustomTypeInfo>(); 

    //Table of all Db objects; accessed by entity-model object as key 
    private Dictionary<object, DbModelObjectBase> _allObjects = new Dictionary<object, DbModelObjectBase>();

    // Cache - we use ObjectCache for compiled queries; we don't need cache entries to expire, but we want to limit cache size automatically
    // ObjectCache provides this cleanup on overflow functionality
    public readonly ObjectCache<string, SqlStatement> QueryCache
      = new ObjectCache<string, SqlStatement>(expirationSeconds: 60, maxLifeSeconds: 60 * 5);

    public ICollection<DbTableInfo> Tables {
      get {return _tables;}
    }
    public ICollection<DbSequenceInfo> Sequences {
      get { return _sequences; }
    }
    public ICollection<DbCustomTypeInfo> CustomDbTypes {
      get { return _customTypes; }
    }
    public DbDriver Driver { get { return Config.Driver; } }

    // retrieving table by name
    public DbTableInfo GetTable(string schema, string tableName) {
      var key = FormatFullName(schema, tableName);
      if(_tablesByName.TryGetValue(key, out DbTableInfo table))
        return table;
      return null; 
    }
    public DbTableInfo GetTable(string fullName) {
      if(_tablesByName.TryGetValue(fullName, out DbTableInfo table))
        return table;
      return null;
    }
    public void RefreshTablesByName() {
      _tablesByName.Clear(); 
      foreach(var t in _tables) {
        _tablesByName.Add(t.FullName, t); 
      }
       
    }
    // Pseudo table used in queries without tables like 'Select 1';
    DbTableInfo _nullTable;


    /// <summary>Constructs DbModel from EntityModel.</summary>
    /// <param name="entityApp"></param>
    /// <param name="config"></param>
    public DbModel(EntityApp entityApp, DbModelConfig config) : this(config) {
      Util.Check(entityApp != null, "entityApp parameter may not be null.");
      EntityApp = entityApp;
      //Add schemas 
      foreach(var area in entityApp.Areas) {
        Schemas.Add(new DbSchemaInfo(this, config.GetSchema(area)));
      }
      VersionInfo = new DbVersionInfo(EntityApp, Config);
      var nullEnt = EntityApp.Model.NullEntityInfo; 
      _nullTable = new DbTableInfo(this, null, "!!NullTable", nullEnt);

      InitSqlParamNames(Driver.SqlDialect.DynamicSqlParameterPrefix, Driver.SqlDialect.MaxParamCount);
    }

    /// <summary>Constructs an empty DbModel. This constructor is used for models loaded from database. </summary>
    /// <param name="config">DB model config object.</param>
    public DbModel(DbModelConfig config) {
      Config = config;
    }

    #region Construction-time methods
    public void AddTable(DbTableInfo table) {
      var key = table.FullName;
      Util.Check(!_tablesByName.ContainsKey(key), "Duplicate table in DbModel: {0}", table.FullName);
      _tables.Add(table);
      _tablesByName.Add(key, table); 
    }

    public void AddSequence(DbSequenceInfo sequence) {
      var oldSeq = _sequences.FirstOrDefault(s => s.FullName == sequence.FullName);
      Util.Check(oldSeq == null, "Duplicate sequence in DbModel: {0}", sequence.FullName);
      _sequences.Add(sequence);
    }

    public void AddCustomType(DbCustomTypeInfo type) {
      _customTypes.Add(type);
    }

    public void RegisterDbObject(object entityModelObject, DbModelObjectBase dbObject) {
      if (entityModelObject == null) return;
      if (dbObject.ObjectType == DbObjectType.Column)
        return; // do not register columns, there are too many of them, and columns are not real standalone objects
      _allObjects[entityModelObject] = dbObject; 
    }//method
    #endregion

    public string FormatFullName(string schema, string name) {
      return Driver.SqlDialect.FormatFullName(schema, name);
    }

    public T LookupDbObject<T>(object key, bool throwNotFound = false) where T : DbModelObjectBase {
      DbModelObjectBase result;
      _allObjects.TryGetValue(key, out result);
      if(result == null && throwNotFound)
        Util.Throw("Failed to lookup DbObject for entity model object {0}", key);
      return (T)result;
    }
    
    public DbTableInfo GetTable(Type entityType, bool throwIfNotFound = true) {
      var entInfo = (EntityInfo) this.EntityModel.GetEntityInfo(entityType, throwIfNotFound);
      if (entInfo == null)
        return null;
      if(entityType == typeof(INullEntity)) {
        return _nullTable; 
      }
      var tableInfo = this.LookupDbObject<DbTableInfo>(entInfo, throwIfNotFound);
      return tableInfo;
    }

    public DbSequenceInfo GetSequence(SequenceDefinition sequence) {
      var dbSeq = Sequences.FirstOrDefault(s => s.Definition == sequence);
      Util.Check(dbSeq != null, "DB Sequence {0} not found. ", sequence?.Name);
      return dbSeq;
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

    
    internal void ResetPeerRefs() {
      foreach (var t in this.Tables) {
        t.Peer = null;
        foreach (var c in t.Columns)
          c.Peer = null;
        foreach (var k in t.Keys)
          k.Peer = null;
        foreach (var rc in t.RefConstraints)
          rc.Peer = null;
      }
      foreach (var seq in this.Sequences)
        seq.Peer = null;
      foreach(var tp in this.CustomDbTypes)
        tp.Peer = null; 
    }

    #region Cached SQL param names
    // We pre-create parameter names to avoid string allocations at runtime
    string[] _sqlParamNames;
    protected void InitSqlParamNames(string prefix, int maxParamCount) {
      // Create array of param names
      _sqlParamNames = new string[maxParamCount + 100]; // +100 just in case; and to make unit test easier
      for(int i = 0; i < _sqlParamNames.Length; i++)
        _sqlParamNames[i] = prefix + i;
    }

    public string GetSqlParameterName(int index) {
      return _sqlParamNames[index];
    }
    #endregion 




  }//class

}
