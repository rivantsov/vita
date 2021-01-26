using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Model;
using Vita.Entities.Utilities;

namespace BookStore {
  /// <summary>Sample naming policy. Changes all column and table names to lower-case with underscores. 
  /// This might be handy in Postgres, in this case low/upper letters do not matter in custom SQLs. 
  /// see discussion here: https://github.com/rivantsov/vita/issues/38
  /// Note: custom naming does not work for view columns, some compilcations with SQL translation.
  /// Try defining view entity with desired low-case member names
  /// </summary>
  public class AllLowCaseNamingPolicy : IDbNamingPolicy {
    HashSet<string> _schemas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public AllLowCaseNamingPolicy(params string[] schemas) {
      _schemas.UnionWith(schemas); 
    }

    public void CheckName(object dbObject) {
      switch(dbObject) {
        case DbTableInfo table: // called for tables only, not views
          if (_schemas.Contains(table.Schema))
            table.TableName = StringHelper.ToUnderscoreAllLower(table.TableName); 
          break;
        case DbColumnInfo column:
          if(_schemas.Contains(column.Table.Schema))
            column.ColumnName = StringHelper.ToUnderscoreAllLower(column.ColumnName); 
          break;
        case DbKeyInfo key:
          if (_schemas.Contains(key.Table.Schema))
            key.Name = StringHelper.ToUnderscoreAllLower(key.Name);
          break; 
      }
    }

  } //class
} //ns
