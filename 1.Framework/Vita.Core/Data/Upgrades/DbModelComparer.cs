using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Logging;
using Vita.Data.Driver;
using Vita.Data.Model;

namespace Vita.Data.Upgrades {

  public partial class DbModelComparer {
    DbUpgradeInfo _upgradeInfo;
    DbModel _newModel, _oldModel;
    DbUpgradeOptions _options;
    bool _useRefIntegrity, _compareTables, _compareIndexes, _compareViews, _compareStoredProcs, 
         _supportsSchemas, _supportsOrderInIndexes, _dropUnknown;
    MemoryLog _log;

    //global list of columns in old tables that changed
    HashSet<DbColumnInfo> _changedColumns = new HashSet<DbColumnInfo>();
    // We use global list of changed keys to check the ref constraints for changes - 
    // if FromKey or ToKey changed, then ref constraint must be regenerated
    HashSet<DbKeyInfo> _changedKeys = new HashSet<DbKeyInfo>();

    public void AddDbModelChanges(DbUpgradeInfo upgradeInfo, MemoryLog log) {
      _upgradeInfo = upgradeInfo;
      _newModel = upgradeInfo.NewDbModel;
      _oldModel = upgradeInfo.OldDbModel;
      _log = log;
      _options = _upgradeInfo.Settings.UpgradeOptions; 
      var driver = _newModel.Driver;
      _useRefIntegrity = driver.Supports(DbFeatures.ReferentialConstraints) && _newModel.Config.Options.IsSet(DbOptions.UseRefIntegrity) ;
      _compareTables = _options.IsSet(DbUpgradeOptions.UpdateTables);
      _compareIndexes = _options.IsSet(DbUpgradeOptions.UpdateIndexes);
      _compareViews = driver.Supports(DbFeatures.Views) && _options.IsSet(DbUpgradeOptions.UpdateViews);
      var usesStoredProcs = driver.Supports(DbFeatures.StoredProcedures) && _newModel.Config.Options.IsSet(DbOptions.UseStoredProcs);
      _compareStoredProcs = usesStoredProcs && _options.IsSet(DbUpgradeOptions.UpdateStoredProcs);
      _dropUnknown = _options.IsSet(DbUpgradeOptions.DropUnknownObjects);
      _supportsSchemas = driver.Supports(DbFeatures.Schemas);
      _supportsOrderInIndexes = driver.Supports(DbFeatures.OrderedColumnsInIndexes);

      // Nullify all obj.Peer fields to make sure we drop references to old model - mostly convenience in debugging
      // to allow multiple entry into this method in debugger
      _oldModel.ResetPeerRefs(); 
      MatchObjectsWithPeers();

      //new stuff
      BuildChangeList();

      // Do not do it here, refs to old objects might be need by Sql generators; we will reset refs after completing update
      // _newModel.ResetPeerRefs() 
    }//class

    // Returns true if schema is present in New (current model); by default we do not do anything with 
    //   objects in inactive schemas 
    private bool IsActive(string schema) {
      if (!_supportsSchemas && string.IsNullOrEmpty(schema)) 
        return true; 
      return _upgradeInfo.NewDbModel.ContainsSchema(schema);
    }

    #region Matching objects - set Peer property on each object in new and old models
    private void MatchObjectsWithPeers() {
      //Go through tables in new model and try to find match in old model
      foreach(var newT in _newModel.Tables) {
        var oldT = FindOldTable(newT);
        if (oldT == null) 
          continue; //next table
        //We found matching table - link them; then match columns and keys in both tables
        oldT.Peer = newT;
        newT.Peer = oldT; 
        
        // Match columns
        foreach (var newCol in newT.Columns) {
          var oldCol = FindOldColumn(newCol, oldT);
          if (oldCol == null) continue; 
          oldCol.Peer = newCol;
          newCol.Peer = oldCol; 
        }//foreach newCol

        //Match keys
        foreach (var newK in newT.Keys) {
          //first by name      
          var oldK = FindOldKey(oldT, newK);
          if (oldK == null) 
            continue; 
          oldK.Peer = newK;
          newK.Peer = oldK;
        }//foreach newKey
      } //foreach newT

      // Match ref constraints - in a separate loop, after all tables and keys are matched
      foreach (var newT in _newModel.Tables) {
        //Detect changes - only if there's object in old model
        if (newT.Peer == null) continue;
        foreach (var newRc in newT.RefConstraints) {
          var oldRc = FindOldRefConstraint(newT.Peer, newRc);
          if (oldRc == null) 
            continue; 
          oldRc.Peer = newRc;
          newRc.Peer = oldRc; 
        } //foreach newRc
      }//foreach oldT

      //Match stored procs
      if (_compareStoredProcs) {
        foreach (var oldCmd in _oldModel.Commands) {
          var newCmd = _newModel.GetCommand(oldCmd.FullCommandName);
          oldCmd.Peer = newCmd;
          if (newCmd != null)
            newCmd.Peer = oldCmd;
        }
      }//if 

      //Sequences
      foreach (var dbSeq in _newModel.Sequences) {
        var oldSeq = dbSeq.Peer = _oldModel.Sequences.FirstOrDefault(s => s.FullName == dbSeq.FullName);
        if (oldSeq != null)
          oldSeq.Peer = dbSeq; 
      }
      
      //Custom db types
      foreach (var tp in _newModel.CustomDbTypes) {
        var oldType = _oldModel.CustomDbTypes.FirstOrDefault(t => t.FullName == tp.FullName);
        if (oldType != null) {
          tp.Peer = oldType;
          oldType.Peer = tp; 
        }
      }//foreach tp

    }//method

    // Utility methods used in matching ----------------------------------------------------------
    private DbTableInfo FindOldTable(DbTableInfo newTable) {
      DbTableInfo oldT = _oldModel.GetTable(newTable.FullName);
      if (oldT != null) return oldT;
      var entity = newTable.Entity;
      if(entity == null) return null; //just in case, if we ever have tables without entities
      if (entity.OldNames == null) return null; 
      foreach (var oldName in entity.OldNames) {
        oldT = _oldModel.GetTable(newTable.Schema, oldName);
        if (oldT != null)  return oldT;
        //if old name starts with "I" (like all interfaces), then try without I
        if (oldName.StartsWith("I"))
          oldT = _oldModel.GetTable(newTable.Schema, oldName.Substring(1));
        if (oldT != null)  return oldT;
      }//foreach oldName
      return null; 
    }
    private DbColumnInfo FindOldColumn(DbColumnInfo newCol, DbTableInfo oldTable) {
      var oldCol = oldTable.Columns.FirstOrDefault(c => c.ColumnName == newCol.ColumnName);
      if (oldCol != null) return oldCol;
      // Try finding by old name
      var oldNames = newCol.Member.OldNames;
      if (oldNames == null)  return null; 
      foreach (var oldName in oldNames) {
        oldCol = oldTable.Columns.FirstOrDefault(c => c.ColumnName == oldName);
        if (oldCol != null)
          return oldCol; 
      }
      return null; 
    }

    private DbKeyInfo FindOldKey(DbTableInfo oldTable, DbKeyInfo newKey) {
      // just for easier debugging, preselect keys matching by type and column count
      var similarOldKeys = oldTable.Keys.Where(k => k.KeyType == newKey.KeyType && k.KeyColumns.Count == newKey.KeyColumns.Count).ToList();
      foreach (var oldKey in similarOldKeys) {
        // If we have duplicating keys (ex: Northwind database, OrdersTable, key CustomerID, CustomerOrders), then lets try match them in pairs, to accurately report differences
        if(oldKey.Peer == newKey)
          return oldKey;
        if(oldKey.Peer != null) 
          continue;
        if (DbKeysMatch(oldKey, newKey))
          return oldKey;
      }
      return null;
    }

    private bool DbKeysMatch(DbKeyInfo oldKey, DbKeyInfo newKey) {
      if(oldKey.KeyType != newKey.KeyType ||
          oldKey.KeyColumns.Count != newKey.KeyColumns.Count) return false;
      //check column-by-column match
      for(int i = 0; i < oldKey.KeyColumns.Count; i++) {
        var oldKeyCol = oldKey.KeyColumns[i];
        var newKeyCol = newKey.KeyColumns[i];

        if(oldKeyCol.Column.Peer != newKeyCol.Column)
          return false; 
        if(_supportsOrderInIndexes && oldKeyCol.Desc != newKeyCol.Desc)
          return false;
      }
      // check filter and included columns
      if(_newModel.Driver.Supports(DbFeatures.FilterInIndexes) && (NormalizeIndexFilter(oldKey.Filter) != NormalizeIndexFilter(newKey.Filter)))
        return false;
      if(_newModel.Driver.Supports(DbFeatures.IncludeColumnsInIndexes)) {
        //compare lists - first counts, then columns; note that columns might be in a different order
        if(oldKey.IncludeColumns.Count != newKey.IncludeColumns.Count)
          return false; 
        foreach(var oldIncCol in oldKey.IncludeColumns)
          if(oldIncCol.Peer == null || !newKey.IncludeColumns.Contains(oldIncCol.Peer))
            return false; 
      }//if 
      return true; 
    }

    // Some servers (MS SQL) injects extra parenthesis into filter, so when we read back the filter, 
    // it is slightly different from what was specified. We normalize it by simply removing all spaces and parenthesis
    private string NormalizeIndexFilter(string filter) {
      if(string.IsNullOrWhiteSpace(filter))
        return null;
      var nf = filter.Replace("(", string.Empty).Replace(")", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
      return nf; 

    }

    private DbRefConstraintInfo FindOldRefConstraint(DbTableInfo oldTable, DbRefConstraintInfo newRefConstraint) {
      var newFrom = newRefConstraint.FromKey;
      var newTo = newRefConstraint.ToKey;
      foreach (var oldRc in oldTable.RefConstraints)
        if (oldRc.FromKey.Peer == newFrom && oldRc.ToKey.Peer == newTo)
          return oldRc;
      return null;
    } 
    #endregion

    #region Building changes
    private void BuildChangeList() {
      _changedColumns.Clear();
      _changedKeys.Clear();
      //Schemas
      if(_supportsSchemas) {
        /* - disabled, dropping schemas brings more trouble than it's worth
        //Old schemas to delete
        foreach(var oldSch in _oldModel.Schemas)
          if(!_newModel.ContainsSchema(oldSch.Name))
            _changeSet.AddChange(oldSch, null);
         */ 
        //New schemas to create
        foreach(var newSch in _newModel.Schemas)
          if(IsActive(newSch.Schema) && !_oldModel.ContainsSchema(newSch.Schema))
            _upgradeInfo.AddChange(null, newSch);
      }

      if (_compareTables) {
        // Tables - first go thru tables in old model
        foreach (var oldTbl in _oldModel.Tables) {
          if (!IsActive(oldTbl.Schema)) 
            continue;
          var tblChangesGrp = AnalyzeTableChanges(oldTbl);
          if (tblChangesGrp != null && tblChangesGrp.Changes.Count > 0) //if there are any changes inside, add tableChangeGroup
            _upgradeInfo.TableChanges.Add(tblChangesGrp);
        }//foreach oldTbl
        // New tables -------------------------------------------------------------------------
        foreach (var newTbl in _newModel.Tables) {
          if (!IsActive(newTbl.Schema)) continue;
          if (newTbl.Peer != null)
            continue; // we already processed it as table being changed
          BuildNewTableChangeGroup(newTbl);
        }//foreach newTbl
      } //if _compareTables

      //Ref constraints
      if(_useRefIntegrity) {
        foreach (var oldT in _oldModel.Tables) {
          if (!IsActive(oldT.Schema)) continue;
          if (oldT.Peer == null && !_dropUnknown)
            continue; //ignore table 
          foreach (var refC in oldT.RefConstraints)
            if (refC.Peer == null || RefConstraintChanged(refC))
              _upgradeInfo.AddChange(refC, refC.Peer);
        }
        foreach (var newT in _newModel.Tables) {
          if(!IsActive(newT.Schema)) continue;
          foreach(var refC in newT.RefConstraints)
            if (refC.Peer == null)
              _upgradeInfo.AddChange(null, refC);
        }
      } //if _useRefIntegrity

      //Sequences
      foreach (var seq in _newModel.Sequences) {
        if (seq.Peer == null)
          _upgradeInfo.AddChange(null, seq);
      }
      //Custom db types; we use it only for MS SQL, to add utility type (VITA_ArrayAsTable) that is used to pass arrays in parameters
      foreach (var tp in _newModel.CustomDbTypes)
        if (tp.Peer == null)
          _upgradeInfo.AddChange(null, tp); 
    }//method


    //Analyzes changes in columns and keys, but not ref constraints
    private DbTableChangeGroup AnalyzeTableChanges(DbTableInfo oldTable) {
      var tableChangeGrp = new DbTableChangeGroup(oldTable, oldTable.Peer);
      if(oldTable.Peer == null) {
        //Table deleted -----------------------------------------------------------------------------------
        if(_dropUnknown) {
          tableChangeGrp.Changes.Add(new DbObjectChange(oldTable, null, DbObjectChangeType.Drop));
          //delete keys - all except primary key - it is deleted automatically with the table; foreign keys are deleted with constraints
          foreach(var oldKey in oldTable.Keys) {
            _changedKeys.Add(oldKey);
            if(!oldKey.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey))
              tableChangeGrp.AddChange(oldKey, null);
          }
          foreach(var cmd in oldTable.CrudCommands)
            tableChangeGrp.AddChange(cmd, null); 
        }
        return tableChangeGrp;
      } 

      //Table/View modified? -----------------------------------------------------------------------------------
      var newTable = oldTable.Peer;
      if(_compareTables) {
        //Check table rename
        string descr;
        if(!NamesMatch(oldTable.TableName, oldTable.Peer.TableName)) {
          descr = string.Format("Renamed {0} to {1}", oldTable.TableName, oldTable.Peer.TableName);
          var tableRename = new DbObjectChange(oldTable, oldTable.Peer, DbObjectChangeType.Rename, descr);
          tableChangeGrp.Changes.Add(tableRename);
        }
        // Internals of the table or view
        switch(oldTable.Kind) {
          case EntityKind.View:
            if (!ViewsMatch(oldTable, oldTable.Peer)) {
              tableChangeGrp.AddChange(oldTable, null, DbObjectChangeType.Drop);
              tableChangeGrp.AddChange(null, oldTable.Peer, DbObjectChangeType.Add);
            }
            break; 
          case EntityKind.Table:
            foreach(var oldCol in oldTable.Columns) {
              if(oldCol.Peer == null) {
                _changedColumns.Add(oldCol);
                tableChangeGrp.AddChange(oldCol, null);
              } else {
                //Check column rename
                if(!NamesMatch(oldCol.ColumnName, oldCol.Peer.ColumnName))
                  tableChangeGrp.AddChange(oldCol, oldCol.Peer, DbObjectChangeType.Rename);
                if(!ColumnsMatch(oldCol, oldCol.Peer, out descr)) {
                  _changedColumns.Add(oldCol);
                  tableChangeGrp.AddChange(oldCol, oldCol.Peer, notes: descr);
                }
              }
            } //foreach col
            //new columns
            foreach (var newCol in newTable.Columns)
              if (newCol.Peer == null) //if not null - it is already taken care of
                tableChangeGrp.AddChange(null, newCol);
            break; 

        }//switch oldTable.Kind
        //Check table columns

      }// if compareTables

      //Detect all changed keys, indexes; skip Foreign keys - they're not real keys
      // We do not modify keys, only drop/create them if anything mismatches; 
      // key.Peer is set only if the keys did not change and non of the columns changed
      foreach (var key in oldTable.Keys) {
        bool changed = key.Peer == null || KeyChanged(key);
        if (!changed) continue; 
        _changedKeys.Add(key); 
        if (key.KeyType.IsSet(KeyType.ForeignKey) || IsPureIndex(key) && !_compareIndexes)
           continue; 
        tableChangeGrp.AddChange(key, key.Peer);
      }
      foreach (var key in newTable.Keys) {
        if (key.Peer != null)
          continue; //if Peer != null, it is already included in previous loop
        if (key.KeyType.IsSet(KeyType.ForeignKey) || IsPureIndex(key) && !_compareIndexes)
           continue; 
        tableChangeGrp.AddChange(null, key);
      }

      // CRUD stored procs
      if(_compareStoredProcs) {
        var oldUseProcs = _oldModel.Config.Options.IsSet(DbOptions.UseStoredProcs);
        var newUseProcs = _newModel.Config.Options.IsSet(DbOptions.UseStoredProcs);
        if(oldUseProcs || newUseProcs) {
          var forceRebuildProcs = tableChangeGrp.Changes.Count > 0;
          foreach(var cmd in newTable.CrudCommands) {
            if (cmd.CommandType != System.Data.CommandType.StoredProcedure)
              continue; 
            if(cmd.Peer != null && StoredProceduresMatch(cmd.Peer, cmd))
              continue;
            tableChangeGrp.AddChange(cmd.Peer, cmd);
          }
        }//if useProcs
        // End table modified case ------------------------------------------------------------------------
      }//stored procs
      return tableChangeGrp;
    }

    private bool ViewsMatch(DbTableInfo existing, DbTableInfo newView) {
      if (existing.IsMaterializedView != newView.IsMaterializedView)
        return false;
      return newView.ViewHash == existing.ViewHash; 
    }

    private DbTableChangeGroup BuildNewTableChangeGroup(DbTableInfo newTable) {
      var tableChangeGroup = new DbTableChangeGroup(null, newTable);
      _upgradeInfo.TableChanges.Add(tableChangeGroup);
      tableChangeGroup.Changes.Add(new DbObjectChange(null, newTable));
      // add all keys in new table
      foreach(var key in newTable.Keys) {
        _changedKeys.Add(key); 
        if(!key.KeyType.IsSet(KeyType.ForeignKey))
          tableChangeGroup.AddChange(null, key);
      }
      // Stored procs
      // About referencing old proc in cmd.Peer: the table is new, so there should be normally no old table and no old stored procs; 
      // However, there might be a situation when old stored proc is left after we delete table. In this case we need to drop old stored proc
      // before we create new one. Stored procs are matched by name, so newCmd would have old proc in Peer field.
      if(_compareStoredProcs && newTable.CrudCommands.Count > 0)
        foreach(var cmd in newTable.CrudCommands)
          tableChangeGroup.AddChange(cmd.Peer, cmd);
      return tableChangeGroup;
    }
    #endregion


    #region Helpers, utility methods
    private bool RefConstraintChanged(DbRefConstraintInfo constraint) {
      return _changedKeys.Contains(constraint.FromKey) || _changedKeys.Contains(constraint.ToKey);
    }

    private bool IsPureIndex(DbKeyInfo key) {
      bool isPkOrFk = key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey);
      return !isPkOrFk; 
    }

    private bool KeyChanged(DbKeyInfo keyInfo) {
      if(keyInfo.Peer == null)
        return true;
      var supportsOrder = _newModel.Driver.Supports(DbFeatures.OrderedColumnsInIndexes);
      var supportsInclude = _newModel.Driver.Supports(DbFeatures.IncludeColumnsInIndexes);
      // Check column counts
      if(keyInfo.KeyColumns.Count != keyInfo.Peer.KeyColumns.Count)
        return true;
      if(supportsInclude && keyInfo.IncludeColumns.Count != keyInfo.Peer.IncludeColumns.Count)
        return true;
      //Check if any column changed
      if(keyInfo.KeyColumns.Any(kc => _changedColumns.Contains(kc.Column)))
        return true;
      if(supportsInclude)
        if(keyInfo.IncludeColumns.Any(c => _changedColumns.Contains(c)))
          return true;
      // compare individual columns match
      for(int i = 0; i < keyInfo.KeyColumns.Count; i++) {
        var oldKeyCol = keyInfo.KeyColumns[i];
        var newKeyCol = keyInfo.Peer.KeyColumns[i];
        if(oldKeyCol.Column != newKeyCol.Column.Peer)
          return true;
        if(supportsOrder)
          if(oldKeyCol.Desc != newKeyCol.Desc)
            return true;
      }//for i
      //check included columns list match
      if(supportsInclude)
        for(int i = 0; i < keyInfo.IncludeColumns.Count; i++) {
          var oldCol = keyInfo.IncludeColumns[i];
          var newCol = keyInfo.Peer.IncludeColumns[i];
          if(oldCol.Peer != newCol)
            return true;
        }
      // if everyting matched, key did not change
      return false;
    }

    private static bool StoredProceduresMatch(DbCommandInfo oldSp, DbCommandInfo newSp) {
      return newSp.SourceHash == oldSp.SourceHash; 
    }

    private bool ColumnsMatch(DbColumnInfo oldColumn, DbColumnInfo newColumn, out string description) {
      description = null;
      if(newColumn.Member.Flags.IsSet(EntityMemberFlags.AsIs))
        return true; 
      bool result = true;
      var oldSpec = oldColumn.TypeInfo.SqlTypeSpec;
      var newSpec = newColumn.TypeInfo.SqlTypeSpec;
      if(oldSpec != newSpec) {
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
    }//method
    #endregion


    private static bool NamesMatch(string x, string y) {
      return string.Compare(x, y, ignoreCase: true) == 0; 
    }

  } //class

}//namespace
