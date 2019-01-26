using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Linq;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;

namespace Vita.Data.Runtime {

  public class SqlCacheKey {
    IList<string> _strings;

    const string _CRUD = "CRUD";
    const string _LINQ = "LINQ";

    // used for LINQ statements when values are added dynamically as we analyze the query
    private SqlCacheKey(IList<string> strings) {
      _strings = strings; 
    }

    // fixed-size cache key, used for CRUD commands, where key is known in advance, it is fixed limited list
    private SqlCacheKey(params string[] strings) {
      _strings = strings;
    }

    public static SqlCacheKey CreateForLinq(LinqCommandKind linqKind, LockType lockType, QueryOptions options) {
      var strings = new List<string>(100); //we have to create real list and reserve capacity for more values
      strings.Add(_LINQ);
      strings.Add(linqKind.ToString());
      strings.Add(options.ToString());
      return new SqlCacheKey(strings); 
    }

    public static SqlCacheKey CreateForSelectByKey(EntityKeyInfo key, LockType lockType, EntityMemberMask mask) {
      var tag = key.KeyType.IsSet(KeyType.PrimaryKey) ? "SELECT-BY-PK" : "SELECT-BY-KEY";
      return new SqlCacheKey(_CRUD, key.Entity.Name, tag, key.Name, lockType.ToString(), mask.Bits.ToHex()); 
    }

    public static SqlCacheKey CreateForCrud(EntityInfo entity, EntityStatus status, EntityMemberMask mask) {
      switch(status) {
        case EntityStatus.New:
          return new SqlCacheKey(_CRUD, entity.Name, "INSERT-ONE");
        case EntityStatus.Modified:
          return new SqlCacheKey(_CRUD, entity.Name, "UPDATE-ONE", mask.Bits.ToHex());
        case EntityStatus.Deleting:
          return new SqlCacheKey(_CRUD, entity.Name, "DELETE-ONE");
        default:
          Util.Throw("Invalid entity status, entity: {0}", entity.Name);
          return null; //never happens
      }
    }

    public SqlCacheKey CreateForDeleteMany(EntityInfo entity) {
      return new SqlCacheKey("CRUD", entity.Name, "DELETE-MANY");
    }
  }

}
