using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Linq;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;

namespace Vita.Data.Sql {


  public class SqlCacheKeyBuilder {
    List<string> _strings; //might be array or list
    string _key;

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
    public SqlCacheKeyBuilder(params string[] values) {
      _strings = new List<string>(100);
      if(values != null)
        _strings.AddRange(values); 
    }

    // static helpers

    public static string BuildSpecialSelectKey(string subType, string entityName, string keyName, LockType lockType,
                                                IList<EntityKeyMemberInfo> orderBy) {
      var ordStr = (orderBy == null) ? "none" : string.Join(",", orderBy);
      var key = $"CRUD-SELECT/{subType}/{entityName}/key:{keyName}/Lock:{lockType}/OrderBy:{ordStr}";
      return key; 
    }

    public static string BuildCrudKey(EntityStatus status, string entityName, EntityMemberMask mask) {
      var maskStr = status == EntityStatus.Modified ? mask.AsHexString() : "(nomask)";
      var key = $"CRUD/{entityName}/{status}/Mask:{maskStr}";
      return key;
    }
    public static string BuildCrudDeleteManyKey(string entityName) {
      var key = $"CRUD/DELETE-MANY/{entityName}";
      return key;
    }


  }
}
