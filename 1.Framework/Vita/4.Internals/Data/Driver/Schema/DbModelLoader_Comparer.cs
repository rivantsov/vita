using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities.Model;

namespace Vita.Data.Driver {

  // methods for object comparison, inheriters can override these

  partial class DbModelLoader : IDbObjectComparer {

    public virtual bool ColumnsMatch(DbColumnInfo oldColumn, DbColumnInfo newColumn, out string description) {
      description = null;
      if(newColumn.Member.Flags.IsSet(EntityMemberFlags.AsIs))
        return true;
      bool result = true;
      var oldSpec = oldColumn.TypeInfo.DbTypeSpec;
      var newSpec = newColumn.TypeInfo.DbTypeSpec;
      if(!oldSpec.Equals(newSpec, StringComparison.OrdinalIgnoreCase)) {
        description = string.Format("SQL data type change from {0} to {1}." + Environment.NewLine, oldSpec, newSpec);
        result = false;
      }
      //SqlTypeSpec in typeinfo does not include nullable flag, so we have to compare it separately.
      var oldIsNullable = oldColumn.Flags.IsSet(DbColumnFlags.Nullable);
      var newIsNullable = newColumn.Flags.IsSet(DbColumnFlags.Nullable);
      if(oldIsNullable != newIsNullable) {
        description += string.Format("Nullable flag change from {0} to {1}." + Environment.NewLine, oldIsNullable, newIsNullable);
        result = false;
      }
      return result;
      //TODO: add check of default/init expression

    }

    public virtual bool KeysMatch(DbKeyInfo oldKey, DbKeyInfo newKey) {
      var model = newKey.DbModel;

      if(oldKey.KeyType != newKey.KeyType ||
          oldKey.KeyColumns.Count != newKey.KeyColumns.Count)
        return false;
      //check column-by-column match
      for(int i = 0; i < oldKey.KeyColumns.Count; i++) {
        var oldKeyCol = oldKey.KeyColumns[i];
        var newKeyCol = newKey.KeyColumns[i];

        if(oldKeyCol.Column.Peer != newKeyCol.Column)
          return false;
        if(model.Driver.Supports(DbFeatures.OrderedColumnsInIndexes) && oldKeyCol.Desc != newKeyCol.Desc)
          return false;
      }

      //Check if any column changed
      if(oldKey.KeyColumns.Any(kc => kc.Column.Flags.IsSet(DbColumnFlags.IsChanging)))
        return true;

      // check filter and included columns
      if(model.Driver.Supports(DbFeatures.FilterInIndexes)) {
        var oldFilter = oldKey.Filter?.DefaultSql;
        var newFilter = newKey.Filter?.DefaultSql;
        if(oldFilter != null || newFilter != null)
          if(NormalizeIndexFilter(oldFilter) != NormalizeIndexFilter(newFilter))
            return false;
      }
      if(model.Driver.Supports(DbFeatures.IncludeColumnsInIndexes)) {
        //compare lists - first counts, then columns; note that columns might be in a different order
        if(oldKey.IncludeColumns.Count != newKey.IncludeColumns.Count)
          return false;
        if(oldKey.IncludeColumns.Any(c => c.Flags.IsSet(DbColumnFlags.IsChanging)))
          return false;
        foreach(var oldIncCol in oldKey.IncludeColumns)
          if(oldIncCol.Peer == null || !newKey.IncludeColumns.Contains(oldIncCol.Peer))
            return false;
      }//if 
      return true;
    }
    // We normalize it by simply removing all spaces and parenthesis
    protected virtual string NormalizeIndexFilter(string filter) {
      if(string.IsNullOrWhiteSpace(filter))
        return null;
      var nf = filter.Replace("  ", " ").Trim().ToLowerInvariant(); // remove double spaces, trim, tolower
      return nf;
    }


    public virtual bool ViewsMatch(DbTableInfo oldView, DbTableInfo newView) {
      if(oldView.IsMaterializedView != newView.IsMaterializedView)
        return false;
      var oldS = oldView.ViewSql;
      var newS = newView.ViewSql;
      if(oldS == null || newS == null)
        return false;
      if(oldS == newS)
        return true;
      oldS = NormalizeViewScript(oldS);
      newS = NormalizeViewScript(newS);
      if(oldS == newS)
        return true;
      return false;
    }

    protected virtual string NormalizeViewScript(string script) {
      return script;
    }

  }
}
