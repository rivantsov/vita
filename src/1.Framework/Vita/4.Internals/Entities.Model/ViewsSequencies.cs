using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Data.Linq;
using Vita.Entities.Model;

namespace Vita.Entities {

  [Flags]
  public enum DbViewOptions {
    None = 0,
    Materialized = 1 << 1,
    /*  Not supported yet
        Insert = 1 << 2,
        Update = 1 << 3,
        Delete = 1 << 4,
     */
  }

  public enum DbViewUpgradeMode {
    AutoOnMismatch = 0,
    CreateOnly,
    Never,
    Always,
  }


  public class ViewDefinition {
    public EntityModule Module;
    public Type EntityType;
    public IQueryable Query;
    public DbViewOptions Options;
    public readonly DbViewUpgradeMode UpgradeMode;
    public string Name;

    public ViewDefinition(EntityModule module, Type entityType, IQueryable query, DbViewOptions options, 
          string name, DbViewUpgradeMode upgradeMode = DbViewUpgradeMode.AutoOnMismatch) {
      Module = module;
      EntityType = entityType;
      Query = query;
      Options = options;
      Name = name;
      UpgradeMode = upgradeMode; 
    }
  }


  public class SequenceDefinition {
    public EntityModule Module;
    public string Name;
    public Type DataType;
    public int StartValue;
    public int Increment;

    public SequenceDefinition(EntityModule module, string name, Type dataType, int startValue = 0, int increment = 0) {
      Util.Check(_validTypes.Contains(dataType), "Invalid sequence data type: ({0}); must be an integer type.", dataType);
      Module = module;
      Name = name;
      DataType = dataType;
      StartValue = startValue;
      Increment = increment;
    }

    static Type[] _validTypes = new Type[] { typeof(Int32), typeof(UInt32), typeof(Int64), typeof(UInt64), typeof(Decimal) };
  }

  public static class ViewHelper {
    /// <summary>Returns an instance of <c>EntitySet&lt;TResult&gt;</c> interface representing a table in the database 
    /// for use LINQ queries not bound to entity session, for example: LINQ queries used for DB Views definition. </summary>
    /// <typeparam name="T">Entity type.</typeparam>
    /// <returns>An instance of an EntitySet.</returns>
    public static EntitySet<T> EntitySet<T>() {
      return new EntitySet<T>( new EntityQueryProvider()); //fake, empty provider
    }

    public static bool IsSet(this DbViewOptions flags, DbViewOptions flag) {
      return (flags & flag) != 0;
    }
  }

}
