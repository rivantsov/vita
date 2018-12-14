using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Driver;

namespace Vita.Data.Model {

  public static class DbModelHelper {

    public static bool IsInt(this DbType dbType) {
      switch (dbType) {
        case DbType.Byte:
        case DbType.Int16:
        case DbType.Int32:
        case DbType.Int64:
        case DbType.UInt16:
        case DbType.UInt32:
        case DbType.UInt64:
          return true;
        default:
          return false;
      }
    }

    public static string EnumToSqlString(object value, Type type) {
      return ((int)value).ToString();
    }
    public static string StringToSqlString(object value, Type type) {
      if (value == null || value == DBNull.Value) return "NULL";
      var sv = (string)value;
      if (sv.Contains("'")) //if contains quotes, then double them
        sv = sv.Replace("'", "''");
      return "'" + sv + "'";
    }

    public static string GetSqlNameListWithOrderSpec(this IList<DbKeyColumnInfo> keyColumns, string tablePrefix = null) {
      var colSpecs = new string[keyColumns.Count];
      for (int i = 0; i < keyColumns.Count; i++) {
        var keyCol = keyColumns[i];
        var desc = keyCol.Desc ? " DESC" : string.Empty;
        var spec = keyCol.Column.ColumnNameQuoted + desc;
        if (!string.IsNullOrEmpty(tablePrefix))
          spec = tablePrefix + "." + spec; 
        colSpecs[i] = spec; 
      }
      return string.Join(", ", colSpecs);
    }

    public static string GetSqlNameList(this IEnumerable<DbKeyColumnInfo> keyColumns, string tablePrefix = null) {
      var cols = keyColumns.Select(kc => kc.Column);
      return GetSqlNameList(cols, tablePrefix); 
    }

    public static string GetSqlNameList(this IEnumerable<DbColumnInfo> columns, string tablePrefix = null) {
      if (string.IsNullOrEmpty(tablePrefix))
        return "\"" + columns.GetNames("\", \"", false) + "\"";
      var delim = "\", " + tablePrefix + ".\"";
      return tablePrefix + ".\"" + columns.GetNames(delim, false) + "\"";
    }

    public static string GetNames(this IEnumerable<DbKeyColumnInfo> keyColumns, string delimiter = null, bool removeUnderscores = false) {
      var cols = keyColumns.Select(kc => kc.Column);
      return GetNames(cols, delimiter, removeUnderscores);
    }
    public static string GetNames(this IEnumerable<DbColumnInfo> columns, string delimiter = null, bool removeUnderscores = false) {
      var names = columns.Select(c => c.ColumnName);
      delimiter = delimiter ?? string.Empty; 
      var result = string.Join(delimiter, names);
      if (removeUnderscores)
        result = result.Replace("_", string.Empty);
      return result;
    }
    public static IList<DbColumnInfo> GetSelectable(this IEnumerable<DbColumnInfo> columns) {
      return columns.Where(col => !col.Member.Flags.IsSet(EntityMemberFlags.Secret)).ToList();
    }
    public static DbColumnInfo FindByName(this IEnumerable<DbColumnInfo> columns, string name) {
      return columns.FirstOrDefault(c => c.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    public static DbKeyInfo FindByName(this IEnumerable<DbKeyInfo> keys, string name) {
      return keys.FirstOrDefault(key => string.Compare(key.Name, name, ignoreCase: true) == 0);
    }
    public static string JoinNames(params string[] names) {
      var nonEmpty = names.Where(n => !string.IsNullOrWhiteSpace(n));
      return string.Join(".", nonEmpty);
    }

    public static DbKeyInfo FindMatchingIndex(DbKeyInfo foreignKey, IList<DbKeyInfo> keys) {
      foreach(var key in keys) {
        if(!key.KeyType.IsSet(KeyType.Index | KeyType.PrimaryKey) || key.KeyColumns.Count < foreignKey.KeyColumns.Count)
          continue;
        //Check columns are the same
        bool match = true;
        for(int i = 0; i < foreignKey.KeyColumns.Count; i++) {
          if(foreignKey.KeyColumns[i].Column != key.KeyColumns[i].Column)
            match = false;
        }
        if(match)
          return key;
      }
      return null;
    }

    public static IList<DbRefConstraintInfo> GetIncomingReferences(this DbTableInfo table) {
      if(table == null)
        return new List<DbRefConstraintInfo>();
      var refs = table.DbModel.Tables.SelectMany(t => t.RefConstraints.Where(r => r.ToKey.Table == table)).ToList();
      return refs;
    }

    public static string GetDefaultSqlAlias(EntityInfo entity) {
      if(entity == null)
        return "t";
      var name = new string(entity.Name.Where(c => char.IsUpper(c)).ToArray()).ToLowerInvariant();
      return name; 
    }

  }//class

}
