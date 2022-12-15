using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Tools.DbFirst {

  partial class DbFirstAppBuilder {

    StringSet _allEntityNames = new StringSet();
    string _viewPrefix = "v";
    string _tablePrefix = string.Empty;

    private string GenerateEntityName(DbTableInfo table) {
      var entName = DbNameToCsName(table.TableName);
      switch (table.Kind) {
        case EntityKind.Table:
          if (string.IsNullOrWhiteSpace(_tablePrefix) && entName.StartsWith(_tablePrefix))
            entName = entName.Substring(_tablePrefix.Length);
          break;
        case EntityKind.View:
          if (!string.IsNullOrWhiteSpace(_viewPrefix) && entName.StartsWith(_viewPrefix))
            entName = entName.Substring(_viewPrefix.Length);
          break;
      }
      if (_config.Options.IsSet(DbFirstOptions.ChangeEntityNamesToSingular))
        entName = StringHelper.Unpluralize(entName);
      entName = "I" + entName;
      //track uniqueness of type names - we might have trouble if we have 2 tables with the same name in different schemas 
      if (_allEntityNames.Contains(entName))
        entName = entName + "_" + table.Schema;
      _allEntityNames.Add(entName);
      return entName;
    }

    private void BuildIndexFilterIncludes(DbKeyInfo dbKey) {
      var entKey = dbKey.EntityKey;
      if (!entKey.KeyType.IsSet(KeyType.Index))
        return;
      if (_dbModel.Driver.Supports(DbFeatures.FilterInIndexes)) {
        if (dbKey.Filter != null && !string.IsNullOrEmpty(dbKey.Filter.DefaultSql))
          entKey.ParsedFilterTemplate = ParseIndexFilter(dbKey.Filter.DefaultSql, entKey.Entity);
      }
      if (_dbModel.Driver.Supports(DbFeatures.IncludeColumnsInIndexes)) {
        // add included members if any
        var incMembers = dbKey.IncludeColumns.Select(c => c.Member).ToList();
        entKey.IncludeMembers.AddRange(incMembers);
      }
    }

    private Type GetMemberType(DbColumnInfo colInfo) {
      var nullable = colInfo.Flags.IsSet(DbColumnFlags.Nullable);
      var typeInfo = colInfo.TypeInfo;
      var mType = typeInfo.ClrType;
      if (mType.IsValueType && nullable)
        mType = ReflectionHelper.GetNullable(mType);
      Type forcedType;
      if (_config.ForceDataTypes.TryGetValue(colInfo.ColumnName, out forcedType))
        return forcedType;
      if (mType == typeof(byte[])) {
        var changeToGuid =
          _config.Options.IsSet(DbFirstOptions.Binary16AsGuid) && typeInfo.Size == 16 ||
          _config.Options.IsSet(DbFirstOptions.BinaryKeysAsGuid) && colInfo.Flags.IsSet(DbColumnFlags.PrimaryKey | DbColumnFlags.ForeignKey);
        if (changeToGuid)
          return typeof(Guid);
      }
      return mType;
    }

    private EntityFilterTemplate ParseIndexFilter(string filterSql, EntityInfo entity) {
      // The filter coming from database has references to column names (without braces like {colName})
      // The filter in Index attribute references entity members using {..} notation. 
      // Technically we should try to convert filter from db into attribute form, but we don't; maybe later
      var entFilter = new EntityFilterTemplate() { Template = StringTemplate.Parse(filterSql) };
      return entFilter;
    }

    private string GetRefMemberName(DbRefConstraintInfo constr) {
      //create ref member
      var ent = constr.FromKey.Table.Entity;
      var targetEnt = constr.ToKey.Table.Entity;
      string memberName;
      if (constr.FromKey.KeyColumns.Count == 1) {
        var fkColName = constr.FromKey.KeyColumns[0].Column.ColumnName;
        memberName = fkColName;
        // cut-off PK name; but first check what is PK column; if it is just Id, then just cut it off. But sometimes PK includes entity name, like book_id; 
        // In this case, cut-off only '_id'
        var cutOffSuffix = constr.ToKey.KeyColumns[0].Column.ColumnName; //book_id?
        var targetTableName = constr.ToKey.Table.TableName;
        if (cutOffSuffix.StartsWith(targetTableName))
          cutOffSuffix = cutOffSuffix.Substring(targetTableName.Length).Replace("_", string.Empty); // "Id"
        if (memberName.EndsWith(cutOffSuffix, StringComparison.OrdinalIgnoreCase)) {
          memberName = memberName.Substring(0, memberName.Length - cutOffSuffix.Length);
          if (string.IsNullOrEmpty(memberName))
            memberName = targetEnt.Name;
        }
        if (memberName == fkColName) //that is a problem - we cannot have dup names; change to target entity name
          memberName = targetEnt.Name;
      } else
        memberName = targetEnt.Name; //without starting "I"
      memberName = DbNameToCsName(memberName);
      memberName = CheckMemberName(memberName, ent, trySuffix: "Ref");
      return memberName; 
    }

    private bool KeyExpandedMembersMatch(EntityKeyInfo key1, EntityKeyInfo key2) {
      if (key1.KeyMembersExpanded.Count != key2.KeyMembersExpanded.Count)
        return false;
      for (int i = 0; i < key1.KeyMembersExpanded.Count; i++)
        if (!key1.KeyMembersExpanded[i].Matches(key2.KeyMembersExpanded[i]))
          return false;
      return true;
    }

    // removes spaces and underscores, changes to sentence case
    private static string DbNameToCsName(string name) {
      if (name == null) return null;
      //Do uppercasing first
      string nameUpper = name;
      if (name.IndexOf('_') == -1)
        nameUpper = name.FirstCap();
      else {
        //Convert to first cap all segments separated by underscore
        var parts = name.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        nameUpper = String.Join(string.Empty, parts.Select(p => p.FirstCap()));
      }
      //Check all chars they are CLR compatible
      var chars = nameUpper.ToCharArray();
      for (int i = 0; i < chars.Length; i++) {
        if (char.IsLetterOrDigit(chars[i]) || chars[i] == '_')
          continue;
        chars[i] = 'X';
      }
      nameUpper = new string(chars);
      return nameUpper;
    }

    private string CheckMemberName(string baseName, EntityInfo entity, string trySuffix = null) {
      var name = baseName;
      if (entity.GetMember(name) == null)
        return name;
      // For entity references we might have occasional match of baseName with the FK column name. To avoid adding numbers, we try to add "Ref" at the end.
      if (!string.IsNullOrEmpty(trySuffix)) {
        name = baseName + trySuffix;
        if (entity.GetMember(name) == null)
          return name;
      }
      // try adding number at the end.
      for (int i = 1; i < 10; i++) {
        name = baseName + i;
        if (entity.GetMember(name) == null)
          return name;
      }
      Util.Throw("Failed to generate property name for entity {0}, base name {1}", entity.Name, baseName);
      return null;
    }


  }
}
