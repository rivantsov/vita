using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

using Vita.Entities;

namespace Vita.Modules.Logging {

  public static class XmlSerializationHelper {

    public class DictEntry {
      public object Key;
      public object Value;
      public override string ToString(){
        return Key + ":" + Value; 
      }
    }
    
    public static string SerializeDictionary<TKey, TValue>(IDictionary<TKey, TValue> dict) {
        var entries = dict.ToList().Select(kv => new DictEntry() { Key = kv.Key, Value = kv.Value }).ToList(); 
      try {
        var writer = new System.IO.StringWriter();
        var xmlSer = new XmlSerializer(typeof(List<DictEntry>));
        xmlSer.Serialize(writer, entries);
        writer.Flush();
        var result = writer.ToString();
        return result; 
      } catch(Exception ex) {
        ex.AddValue("DictContent", string.Join(";", entries));
        throw; 
      }
    } //method

    public static void DeserializeDictionary<TKey, TValue>(string xml, IDictionary<TKey, TValue> toDict) {
      try {
        var xmlSer = new XmlSerializer(typeof(List<DictEntry>));
        var reader = new System.IO.StringReader(xml);
        var entries = (List<DictEntry>) xmlSer.Deserialize(reader);
        foreach(DictEntry entry in entries)
          toDict[(TKey)entry.Key] = (TValue)entry.Value;
      } catch(Exception ex) {
        ex.AddValue("Xml", xml);
        throw; 
      } 
    } //method

  }
}
