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
    public SqlCacheKeyBuilder(params string[] values) {
      _strings = new List<string>(100);
      if(values != null)
        _strings.AddRange(values); 
    }

  }
}
