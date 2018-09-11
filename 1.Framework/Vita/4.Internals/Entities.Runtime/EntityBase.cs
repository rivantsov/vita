using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Model;

namespace Vita.Entities.Runtime {
  // implemented by EntityBase class, base class for IL-emitted entity classes
  public interface IEntityRecordContainer {
    EntityRecord Record { get; }
  }


  // Default base class for entity classes
  public abstract class EntityBase : INotifyPropertyChanged, IEntityRecordContainer {
    public EntityRecord Record;

    EntityRecord IEntityRecordContainer.Record { get { return Record; } }

    public EntityBase() { }
    public EntityBase(EntityRecord record) {
      Record = record; 
    }

    public override string ToString() {
      return Record.ToString(); 
    }

    public override int GetHashCode() {
      return Record.GetHashCode(); 
    }

    public override bool Equals(object other) {
      if(other == null)
        return false;
      if(other == (object)this)
        return true;
      var otherEntBase = other as EntityBase;
      if(otherEntBase == null)
        return false; 
      var otherRec = otherEntBase.Record;
      return (this.Record.PrimaryKey.Equals(otherRec.PrimaryKey));
    }

    #region INotifyPropertyChanged Members
    event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
      add { Record.PropertyChanged += value; }
      remove {  Record.PropertyChanged -= value; }
    }
    #endregion

  }//class

}

