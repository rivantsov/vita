using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Logging;

namespace Vita.Data.Upgrades {

  public partial class DbModelComparer {
    DbUpgradeInfo _upgradeInfo;
    IDbObjectComparer _comparer; 
    DbModel _newModel, _oldModel;
    DbUpgradeOptions _options;
    bool _useRefIntegrity, _compareTables, _compareIndexes, _compareViews, _supportsSchemas, _dropUnknown;
    IActivationLog _log;

    // We use global list of changed keys to check the ref constraints for changes - 
    // if FromKey or ToKey changed, then ref constraint must be regenerated
    HashSet<DbKeyInfo> _changedKeys = new HashSet<DbKeyInfo>();

    public void BuildDbModelChanges(DbUpgradeInfo upgradeInfo, IDbObjectComparer comparer, IActivationLog log) {
      _upgradeInfo = upgradeInfo;
      _comparer = comparer; 
      _log = log;
      _newModel = upgradeInfo.NewDbModel;
      _oldModel = upgradeInfo.OldDbModel;
      _options = _upgradeInfo.Settings.UpgradeOptions; 
      var driver = _newModel.Driver;
      _useRefIntegrity = driver.Supports(DbFeatures.ReferentialConstraints) && _newModel.Config.Options.IsSet(DbOptions.UseRefIntegrity) ;
      _compareTables = _options.IsSet(DbUpgradeOptions.UpdateTables);
      _compareIndexes = _options.IsSet(DbUpgradeOptions.UpdateIndexes);
      _compareViews = driver.Supports(DbFeatures.Views) && _options.IsSet(DbUpgradeOptions.UpdateViews);
      _dropUnknown = _options.IsSet(DbUpgradeOptions.DropUnknownObjects);
      _supportsSchemas = driver.Supports(DbFeatures.Schemas);

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

    private DbKeyInfo FindOldKey(DbTableInfo oldTable, DbKeyInfo newKey) {
      // raw match by type, and column list
      foreach(var oldKey in oldTable.Keys) {
        // we cannot match just by name: for SQLite, PK has fixed name: sqlite_autoindex_<table>_1
        // also Oracle - max key len is 30, not enough to include all table/col names
        // if(NamesMatch(oldKey.Name, newKey.Name)) return oldKey;
        if(oldKey.KeyType != newKey.KeyType || oldKey.KeyColumns.Count != newKey.KeyColumns.Count)
          continue;
        //check cols
        bool match = true; 
        for(int i = 0; i < oldKey.KeyColumns.Count; i++) {
          if (oldKey.KeyColumns[i].Column != newKey.KeyColumns[i].Column.Peer) {
            match = false;
            break; 
          }
        }
        if(match)
          return oldKey; 
      }
      return null; 
    }

    // Utility methods used in matching ----------------------------------------------------------
    private DbTableInfo FindOldTable(DbTableInfo newTable) {
      DbTableInfo oldT = _oldModel.GetTable(newTable.FullName);
      if (oldT != null) return oldT;
      var entity = newTable.Entity;
      if(entity == null) return null; //just in case, if we ever have tables without entities
      if (entity.OldNames == null) return null;
      var appendSchema = this._newModel.Config.Options.IsSet(DbOptions.AddSchemaToTableNames);
      var prefix = appendSchema ? newTable.Schema + "_" : string.Empty; 
      foreach (var oldN in entity.OldNames) {
        var oldName = prefix + oldN; 
        oldT = _oldModel.GetTable(newTable.Schema, oldName);
        if (oldT != null)
          return oldT;
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

    private DbRefConstraintInfo FindOldRefConstraint(DbTableInfo oldTable, DbRefConstraintInfo newRefConstraint) {
      var newFrom = newRefConstraint.FromKey;
      var newTo = newRefConstraint.ToKey;
      foreach (var oldRc in oldTable.RefConstraints)
        if (oldRc.FromKey.Peer == newFrom && oldRc.ToKey.Peer == newTo && oldRc.CascadeDelete == newRefConstraint.CascadeDelete)
          return oldRc;
      return null;
    } 
    #endregion

    #region Building changes
    private void BuildChangeList() {
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
          foreach (var refC in oldT.RefConstraints) {
            // Ref constraints were initially matched with peers. But now we might have detected changes in underlying keys; 
            // in this case the ref constraint should be set for recreation - by clearing Peers
            if(refC.Peer != null && RefConstraintKeysChanged(refC)) {
              refC.Peer.Peer = null;
              refC.Peer = null; 
            }
            // Now check Peer; if null - drop it, and in the next loop the CREATE script will be added
            if(refC.Peer == null)
              _upgradeInfo.AddChange(refC, refC.Peer);
          }//foreach refC in oldT
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
            if (_compareViews && !ViewsMatch(oldTable, oldTable.Peer)) {
              tableChangeGrp.AddChange(oldTable, null, DbObjectChangeType.Drop);
              tableChangeGrp.AddChange(null, oldTable.Peer, DbObjectChangeType.Add);
            }
            break; 
          case EntityKind.Table:
            foreach(var oldCol in oldTable.Columns) {
              if(oldCol.Peer == null) {
                oldCol.Flags |= DbColumnFlags.IsChanging;
                tableChangeGrp.AddChange(oldCol, null);
              } else {
                //Check column rename
                if(!NamesMatch(oldCol.ColumnName, oldCol.Peer.ColumnName))
                  tableChangeGrp.AddChange(oldCol, oldCol.Peer, DbObjectChangeType.Rename);
                if(!_comparer.ColumnsMatch(oldCol, oldCol.Peer, out descr)) {
                  oldCol.Flags |= DbColumnFlags.IsChanging;
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
      foreach (var oldKey in oldTable.Keys) {
        bool changed = oldKey.Peer == null || !_comparer.KeysMatch(oldKey, oldKey.Peer);
        if (!changed)
          continue; 
        _changedKeys.Add(oldKey); 
        if (oldKey.KeyType.IsSet(KeyType.ForeignKey) || IsPureIndex(oldKey) && !_compareIndexes)
           continue; 
        tableChangeGrp.AddChange(oldKey, oldKey.Peer);
      }
      foreach (var key in newTable.Keys) {
        if (key.Peer != null)
          continue; //if Peer != null, it is already included in previous loop
        //ignore primary key on Views - this is artificial attribute, used on CLR side only
        if (newTable.Kind == EntityKind.View && key.KeyType.IsSet(KeyType.PrimaryKey))
          continue; 
        if (key.KeyType.IsSet(KeyType.ForeignKey) || IsPureIndex(key) && !_compareIndexes)
           continue; 
        tableChangeGrp.AddChange(null, key);
      }

      return tableChangeGrp;
    }

    private bool ViewsMatch(DbTableInfo existing, DbTableInfo newView) {
      if (existing.IsMaterializedView != newView.IsMaterializedView)
        return false;
      switch(newView.Entity.ViewDefinition.UpgradeMode) {
        case DbViewUpgradeMode.Never: return true; //pretend they match
        case DbViewUpgradeMode.Always: return false;
        case DbViewUpgradeMode.CreateOnly:
          return existing == null;
        case DbViewUpgradeMode.AutoOnMismatch:
        default:
          return _comparer.ViewsMatch(existing, newView);
      }
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
      return tableChangeGroup;
    }
    #endregion


    #region Helpers, utility methods
    private bool RefConstraintKeysChanged(DbRefConstraintInfo constraint) {
      return _changedKeys.Contains(constraint.FromKey) || _changedKeys.Contains(constraint.ToKey);
    }

    private bool IsPureIndex(DbKeyInfo key) {
      bool isPkOrFk = key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey);
      return !isPkOrFk; 
    }


    #endregion


    private static bool NamesMatch(string x, string y) {
      return string.Compare(x, y, ignoreCase: true) == 0; 
    }

  } //class

}//namespace
