using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Model;

namespace Vita.Modules.DataHistory {
  public static class DataHistoryExtensions {

    public static HistoryAction ToHistoryAction(this EntityStatus status) {
      switch(status) {
        case EntityStatus.New: return HistoryAction.Created;
        case EntityStatus.Deleting: return HistoryAction.Deleted;
        case EntityStatus.Modified:
        default:
          return HistoryAction.Updated;
      }
    }

    public static string SerializeData(this EntityRecord record) {
      var xmlDoc = new XmlDocument();
      var elData = xmlDoc.CreateElement("Data");
      xmlDoc.AppendChild(elData);
      foreach(var m in record.EntityInfo.Members) {
        switch(m.Kind) {
          case MemberKind.Column:
            var value = record.GetValue(m);
            var el = xmlDoc.CreateElement(m.MemberName);
            el.InnerText = ConvertHelper.ValueToString(value);
            elData.AppendChild(el);
            break;
            //other kinds to implement if necessary
        }//switch
      }//foreach m
      return xmlDoc.OuterXml;
    }

    public static DataHistoryEntry ToHistoryEntry(this IDataHistory history, EntityInfo entity) {
      if(history == null)
        return null; 
      var dict = new Dictionary<string, object>();
      var xmlDoc = new XmlDocument();
      xmlDoc.LoadXml(history.EntityData);
      foreach(var nd in xmlDoc.DocumentElement.ChildNodes) {
        var elem = nd as XmlElement;
        if(elem == null) continue;
        var name = elem.Name;
        var member = entity.GetMember(name);
        if(member == null) continue;
        var value = ConvertHelper.ChangeType(elem.InnerText, member.DataType);
        dict[name] = value;
      }
      return new DataHistoryEntry() { HistoryEntry = history, Values = dict };
    }

    public static IDataHistory CreateHistoryEntry(this EntityRecord forRecord) {
      var session = forRecord.Session;
      var hist = session.NewEntity<IDataHistory>();
      hist.TransactionId = session.GetNextTransactionId();
      hist.EntityName = forRecord.EntityInfo.FullName;
      hist.EntityPrimaryKey = forRecord.PrimaryKey.ValuesToString();
      hist.EntityData = forRecord.SerializeData();
      hist.Action = forRecord.Status.ToHistoryAction();
      return hist;
    }//method


  }//class
}
