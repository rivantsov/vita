using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities.Logging;
using Vita.Entities.Runtime;

namespace Vita.Entities.Model.Construction {
  public class EntityCommandBuilder {
    EntityModel _model;
    SystemLog _log; 

    public EntityCommandBuilder(EntityModel model) {
      _model = model;
      _log = _model.App.SystemLog;
    }

    public EntityCommand BuildCrudSelectAllCommand(EntityInfo entity) {
      var cmd = AddCommand(entity.Name + "_SelectAll", "Selects all entities.", EntityCommandKind.SelectAll, entity);
      return cmd; 
    }

    public EntityCommand BuildCrudSelectAllPagedCommand(EntityInfo entity) {
      var cmd = AddCommand(entity.Name + "_SelectAllPaged", "Selects all entities.", EntityCommandKind.SelectAllPaged, entity);
      cmd.Parameters.Add(new EntityCommandParameter("__skiprows", typeof(int), 0, 0));
      cmd.Parameters.Add(new EntityCommandParameter("__maxrows", typeof(int), 0, int.MaxValue));
      return cmd;
    }

    public EntityCommand BuildCrudSelectByKeyCommand(EntityKeyInfo key, string filter = null, string nameSuffix = null) {
      var entity = key.Entity; 
      var procName = entity.Name + "_SelectBy" + key.GetMemberNames(string.Empty, removeUnderscore: true) + nameSuffix;
      var descr = "Selects entity(ies) by key.";
      var cmd = AddCommand(procName, descr, EntityCommandKind.SelectByKey, entity, key);
      cmd.Filter = filter;
      AddParametersFromKey(cmd, key);
      return cmd;
    }

    public EntityCommand BuildCrudSelectByKeyArrayCommand(EntityKeyInfo key) {
      if (key.ExpandedKeyMembers.Count > 1)
        return null; 
      var member0 = key.ExpandedKeyMembers[0].Member;
      var entity = key.Entity;
      var procName = entity.Name + "_SelectByArrayOf_" + key.GetMemberNames(string.Empty, removeUnderscore: true);
      var descr = "Selects entities by array of key values.";
      var cmd = AddCommand(procName, descr, EntityCommandKind.SelectByKeyArray, entity, key);
      var prmType = typeof(IEnumerable<>).MakeGenericType(member0.DataType);
      cmd.Parameters.Add(new EntityCommandParameter("Values", prmType, 0, null));
      return cmd;
    }

    public EntityCommand BuildCrudInsertCommand(EntityInfo entity) {
      var cmd = AddCommand(entity.Name + "_Insert", "Inserts a new entity.", EntityCommandKind.Insert, entity);
      //Add PK first
      AddParametersFromKey(cmd, entity.PrimaryKey);
      // Add the rest
      foreach (var member in entity.Members) {
        if (member.Kind != MemberKind.Column || member.Flags.IsSet(EntityMemberFlags.DbComputed) 
          || member.Flags.IsSet(EntityMemberFlags.PrimaryKey)) // PK is already added
          continue; 
        var prm = new EntityCommandParameter(member);
        cmd.Parameters.Add(prm);
      }
      // set param flags
      foreach (var prm in cmd.Parameters) {
        switch(prm.SourceMember.AutoValueType) {
          case AutoType.RowVersion: 
          case AutoType.Identity:   prm.Direction = ParameterDirection.Output; break;
          default: prm.Direction = ParameterDirection.Input; break; 
        }
      }
      return cmd;
    }

    public EntityCommand BuildCrudUpdateCommand(EntityInfo entity) {
      var cmd = AddCommand(entity.Name + "_Update", "Updates an entity.", EntityCommandKind.Update, entity);
      AddParametersFromKey(cmd, entity.PrimaryKey);
      foreach (var member in entity.Members) {
        if (member.Kind != MemberKind.Column)
          continue; 
        if (!member.Flags.IsSet(EntityMemberFlags.RowVersion) &&  member.Flags.IsSet(EntityMemberFlags.PrimaryKey | EntityMemberFlags.NoDbUpdate | EntityMemberFlags.DbComputed))
          continue;
        cmd.Parameters.Add(new EntityCommandParameter(member));
      }
      // set param flags
      foreach (var prm in cmd.Parameters) {
        switch (prm.SourceMember.AutoValueType) {
          case AutoType.RowVersion: prm.Direction = ParameterDirection.InputOutput; break;
          default: prm.Direction = ParameterDirection.Input; break;
        }
      }
      return cmd;
    }

    // Note: for entities with RowVersion, we do NOT check row version when deleting by ID - just checking row count. 
    // this seems reasonable, no need to check that version matches when we are deleting, just check it is not deleted already.
    public EntityCommand BuildCrudDeleteCommand(EntityInfo entity) {
      var cmd = AddCommand(entity.Name + "_Delete", "Deletes an entity.", EntityCommandKind.Delete, entity);
      AddParametersFromKey(cmd, entity.PrimaryKey);
      return cmd;
    }

    public EntityCommand BuildSelectTargetListManyToManyCommand(ChildEntityListInfo listInfo) {
      //for many to many, create command selecting real entities
      var targetEnt = listInfo.TargetEntity;
      var parentEnt = listInfo.ParentRefMember.Entity;
      var parentRefKey = listInfo.ParentRefMember.ReferenceInfo.FromKey; //FK from link entity to parent entity
      var cmdName = targetEnt.Name + "_SelectBy_" + listInfo.LinkEntity.Name + "_" + listInfo.ParentRefMember.MemberName;
      var cmd = AddCommand(cmdName, "Selects entities by many-to-many link.", EntityCommandKind.SelectByKeyManyToMany, targetEnt, parentRefKey);
      cmd.SelectKeySecondary = listInfo.OtherEntityRefMember.ReferenceInfo.FromKey;
      AddParametersFromKey(cmd, cmd.SelectKey);
      return cmd;
    }

    public void ProcessCustomCommand(EntityCommand command) {
      if (command.TargetEntityInfo == null && !AssignEntityInfo(command)) //it is an error - entity info not found
        return; 
      //Individual processing by command kind
      switch (command.Kind) {
        case EntityCommandKind.CustomSelect: case EntityCommandKind.CustomInsert:
        case EntityCommandKind.CustomUpdate: case EntityCommandKind.CustomDelete:
          //BuildCustomCommandParameters(command); 
          break; 
        case EntityCommandKind.PartialUpdate:
          SetupCustomPartialUpdateCommand(command); 
          break; 
      }//switch
    }

    
    // Private Utilities -----------------------------------------------------------------
    private void SetupCustomPartialUpdateCommand(EntityCommand command) {
      AddParametersFromKey(command, command.TargetEntityInfo.PrimaryKey);
      foreach (var memberName in command.UpdateMemberNames) {
        var member = command.TargetEntityInfo.GetMember(memberName);
        if (member == null) {
          _log.Error("Member {0} not found in entity {1}, partial update command {2}", memberName, command.TargetEntityInfo.FullName, command.CommandName);
          continue; 
        }
        //Skip PK members - forgive programmer if he accidentally includes them into update list
        if (member.Flags.IsSet(EntityMemberFlags.PrimaryKey)) continue; 
        command.Parameters.Add(new EntityCommandParameter(member));        
      }      
    }

    private bool AssignEntityInfo(EntityCommand command) {
      var entInfo = _model.GetEntityInfo(command.TargetEntityType);
      if (entInfo == null) {
        _log.Error("Failed to compile custom command {0}: type {1} is not part of entity model.", command, command.TargetEntityType);
        return false;
      }
      command.TargetEntityInfo = entInfo;
      command.Area = entInfo.Area;
      return true;
    }


    private EntityCommand AddCommand(string commandName, string description, EntityCommandKind kind, EntityInfo entity, EntityKeyInfo selectKey = null) {
      var command = new EntityCommand(commandName, description, kind, entity, selectKey);
      _model.AddCommand(command);
      return command;
    }

    private void AddParametersFromKey(EntityCommand command, EntityKeyInfo key) {
      foreach (var keyMember in key.ExpandedKeyMembers) 
        command.Parameters.Add(new EntityCommandParameter(keyMember.Member));
    }

  }//class

}//ns
