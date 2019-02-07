using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Linq;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;

namespace Vita.Data.Sql {

  public class SqlCacheKeyBuilder {
    IList<string> _strings; //might be array or list
    string _key;

    const string _CRUD = "CRUD";
    const string _LINQ = "LINQ";

    public void Add(string value) {
      _strings.Add(value);
      _key = null; 
    }

    public void Trim(int toLength) {
      if(toLength < _strings.Count) {
        var list = _strings as List<string>;
        Util.Check(list != null, "Fatal: invalid call to SqlCacheKey.Trim, key is fixed size list.");
        list.RemoveRange(toLength, _strings.Count - toLength);
      }
      _key = null;
    }

    public string Key {
      get {
        _key = _key ?? string.Join("/", _strings);
        return _key; 
      }
    }

    public int Length {
      get { return _strings.Count; }
    }

    // used for LINQ statements when values are added dynamically as we analyze the query
    private SqlCacheKeyBuilder() {
      _strings = new List<string>(100); 
    }
    // fixed-size cache key, used for CRUD commands, where key is known in advance, it is fixed limited list (array)
    private SqlCacheKeyBuilder(params string[] strings) {
      _strings = strings; 
    }

    public static SqlCacheKeyBuilder Create(string descriptor) {
      var key = new SqlCacheKeyBuilder();
      key.Add(descriptor); 
      return key;
    }

    public static SqlCacheKeyBuilder CreateForLinq(LinqCommand cmd) {
      var key = new SqlCacheKeyBuilder();
      key.Add(_LINQ);
      key.Add(cmd.Source.ToString());
      key.Add(cmd.Operation.ToString());
      return key; 
    }

    public static SqlCacheKeyBuilder CreateForSelectByKey(EntityKeyInfo key, LockType lockType, EntityMemberMask mask) {
      var tag = key.KeyType.IsSet(KeyType.PrimaryKey) ? "SELECT-BY-PK" : "SELECT-BY-KEY";
      return new SqlCacheKeyBuilder(_CRUD, key.Entity.Name, tag, key.Name, lockType.ToString(), mask.Bits.ToHex()); 
    }

    public static SqlCacheKeyBuilder CreateForCrud(EntityInfo entity, EntityStatus status, EntityMemberMask mask) {
      switch(status) {
        case EntityStatus.New:
          return new SqlCacheKeyBuilder(_CRUD, entity.Name, "INSERT-ONE");
        case EntityStatus.Modified:
          return new SqlCacheKeyBuilder(_CRUD, entity.Name, "UPDATE-ONE", mask.Bits.ToHex());
        case EntityStatus.Deleting:
          return new SqlCacheKeyBuilder(_CRUD, entity.Name, "DELETE-ONE");
        default:
          Util.Throw("Invalid entity status, entity: {0}", entity.Name);
          return null; //never happens
      }
    }

    public static SqlCacheKeyBuilder CreateForDeleteMany(EntityInfo entity) {
      return new SqlCacheKeyBuilder("CRUD", entity.Name, "DELETE-MANY");
    }

  }

}
