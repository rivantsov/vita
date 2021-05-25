using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Vita.Entities.Model;

namespace Vita.Entities {
  
  /// <summary><c>EntityArea</c> represents a database schema object, like 'dbo'. </summary>
  public class EntityArea {
    /// <summary>Entity app reference.</summary>
    public readonly EntityApp App;
    /// <summary>Gets database schema corresponding to this area.</summary>
    public readonly string Name;

    public string OracleTableSpace; 

    // The constructor is internal, use EntityModelSetup.AddArea method 
    internal EntityArea(EntityApp app, string name) {
      App = app;
      Util.CheckNotEmpty(name, "SchemaName may not be empty.");
      // Fix, 2021-05, do NOT convert schema to lower invariant
      Name = name.Trim(); //.ToLowerInvariant();
    }

    /// <summary>Returns string representation of the object. </summary>
    /// <returns>String representing the object.</returns>
    public override string ToString() {
      return Name;
    }
    /// <summary>Returns the hash code of the object. </summary>
    /// <returns>Hash code.</returns>
    public override int GetHashCode() {
      return Name.GetHashCode();
    }

  }//class
}
