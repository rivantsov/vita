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

    private void GenerateEntityMembers(DbTableInfo table) {
      var entInfo = table.Entity;
      // generate entity members
      foreach (var col in table.Columns) {
        var nullable = col.Flags.IsSet(DbColumnFlags.Nullable);
        var memberDataType = GetMemberType(col);
        var memberName = CheckMemberName(DbNameToCsName(col.ColumnName), entInfo);
        var member = col.Member = new EntityMemberInfo(entInfo, EntityMemberKind.Column, memberName, memberDataType);
        member.ColumnName = col.ColumnName;
        // member is added to entInfo.Members automatically in constructor
        if (nullable)
          member.Flags |= EntityMemberFlags.Nullable; // in case it is not set (for strings)
        if (col.Flags.IsSet(DbColumnFlags.Identity)) {
          member.Flags |= EntityMemberFlags.Identity;
          member.AutoValueType = AutoType.Identity;
        }
        //hack for MS SQL
        if (col.TypeInfo.TypeDef.Name == "timestamp")
          member.AutoValueType = AutoType.RowVersion;
        member.Size = (int)col.TypeInfo.Size;
        member.Scale = col.TypeInfo.Scale;
        member.Precision = col.TypeInfo.Precision;
        //Check if we need to specify DbType or DbType spec explicitly
        bool unlimited = member.Size < 0;
        if (unlimited)
          member.Flags |= EntityMemberFlags.UnlimitedSize;
        var typeDef = col.TypeInfo.TypeDef;

        // Detect if we need to set explicity DbType or DbTypeSpec in member attribute
        var dftMapping = _typeRegistry.GetDbTypeInfo(member);
        if (col.TypeInfo.Matches(dftMapping))
          continue; //no need for explicit DbTypeSpec
                    //DbTypeMapping is not default for this member - we need to specify DbType or TypeSpec explicitly
        member.ExplicitDbTypeSpec = col.TypeInfo.DbTypeSpec;
        if (member.Flags.IsSet(EntityMemberFlags.Identity))
          entInfo.Flags |= EntityFlags.HasIdentity;
      }
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
  }
}
