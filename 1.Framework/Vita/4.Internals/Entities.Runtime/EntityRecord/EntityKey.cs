using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Vita.Entities.Utilities;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime {

  public class EntityKey : IEquatable<EntityKey>  {
    public EntityKeyInfo KeyInfo { get; private set; } 
    public object[] Values { get; private set; }

    public EntityKey(EntityKeyInfo keyInfo, EntityRecord record) {
      KeyInfo = keyInfo;
      Values = new object[KeyInfo.ExpandedKeyMembers.Count];
      CopyValues(record); 
    }

    public EntityKey(EntityKeyInfo key, params object[] values) {
      var keyCount = key.KeyMembers.Count; 
      Util.Check(keyCount == values.Length, "Invalid number of key values, expected: {0}, provided: {1}.", keyCount, values.Length);
      KeyInfo = key; 
      Values = values;
    }//constructor

    public void CopyValues(EntityRecord fromRecord) {
      for (int i = 0; i < Values.Length; i++)
        Values[i] = fromRecord.GetValueDirect(KeyInfo.ExpandedKeyMembers[i].Member);
      _hashCode = 0;
      _asString = null; 
    }

    //Verify values and create key
    public static EntityKey Create(EntityKeyInfo key, params object[] values) {
      //Verify
      Util.Check(key.ExpandedKeyMembers.Count == values.Length,
        "EntityKeyValue constructor: values count does not match key members count. Key={0}", key);
      for (int i = 0; i < values.Length; i++) {
        var value = values[i];
        if (value == null) continue; 
        var memberType = key.ExpandedKeyMembers[i].Member.DataType;
        var valueType = value.GetType();
        Util.Check(memberType.GetTypeInfo().IsAssignableFrom(valueType.GetTypeInfo()), "Invalid key value [{0}] (type: {1}); expected {2}.",
             value, valueType, memberType);
      }
      return new EntityKey(key, values); 
    }

    //compute on demand
    int _hashCode;
    public override int GetHashCode() {
      if (_hashCode == 0)
        _hashCode = AsString().GetHashCode();
      return _hashCode;
    }

    public override bool Equals(object obj) {
      var keyObj = obj as EntityKey;
      if (keyObj == null)
        return false;
      return Equals(keyObj); 
    }

    public bool Equals(EntityKey other) {
      if (other.KeyInfo != this.KeyInfo)
        return false;
      //Use efficient calc-once representation. Note - this method is heavily used by loaded records dictionary, to lookup by PK
      return this.AsString() == other.AsString(); 
    }

    //Efficient, calc-once string representation; Note - this method is heavily used by loaded records dictionary, to lookup by PK
    string _asString; 
    public string AsString() {
      if (_asString == null)
        _asString = KeyInfo.Entity.EntityType.Name + "/" + ValuesToString();
      return _asString;
    }
    public override string ToString() {
      if (this.KeyInfo.KeyType.IsSet(KeyType.PrimaryKey)) 
        return AsString(); 
      else if (this.KeyInfo.OwnerMember != null && this.KeyInfo.KeyType == KeyType.ForeignKey)
        return Util.SafeFormat("{0}.{1}/{2}/{3}", this.KeyInfo.Entity.Name, this.KeyInfo.OwnerMember.MemberName, this.KeyInfo.KeyType, ValuesToString());
      else 
        return Util.SafeFormat("{0}/{1}/{2}", this.KeyInfo.Entity.Name, this.KeyInfo.KeyType, ValuesToString());
    }

    public string ValuesToString(string separator = ",") {
      //Fast path
      if (Values.Length == 1) {
        var m0 = KeyInfo.ExpandedKeyMembers[0].Member;
        return m0.ValueToStringRef(m0, Values[0]);
      }
      //full path
      var sv = new string[Values.Length];
      for (int i = 0; i < Values.Length; i++) {
        var m = KeyInfo.ExpandedKeyMembers[i].Member;
        sv[i] = m.ValueToStringRef(m, Values[i]);
      }
      return string.Join(separator, sv);
    }

    public static EntityKey KeyFromString(EntityKeyInfo key, string valueString) {
      if (key.ExpandedKeyMembers.Count == 1) { //shortcut
        var m0 = key.ExpandedKeyMembers[0].Member;
        var v = m0.ValueFromStringRef(m0, valueString);
        var kv = new EntityKey(key, new object[] { v });
        return kv;
      }
      //complex keys
      var sValues = valueString.Split(';');
      var values = new object[key.ExpandedKeyMembers.Count];
      for (int i = 0; i < values.Length; i++) {
        var m = key.ExpandedKeyMembers[i].Member;
        values[i] = m.ValueFromStringRef(m, sValues[i]);
      }
      return new EntityKey(key, values);
    }

    public bool IsNull() {
      if (Values.Length == 1 && Values[0] == DBNull.Value) //fast path, most often
        return true; 
      for (int i = 0; i < KeyInfo.ExpandedKeyMembers.Count; i++) {
        var v = Values[i];
        if (v != null && v != DBNull.Value)
          return false; 
      }
      return true; 
    }

  }//class

}//namespace
