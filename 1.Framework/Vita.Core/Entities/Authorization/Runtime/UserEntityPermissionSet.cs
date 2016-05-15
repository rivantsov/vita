using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization.Runtime {

  //Cumulative set of entity permissions for a given entity type. Computed in context of specific authority (role set)
  internal class UserEntityPermissionSet {
    public Type EntityType;
    public bool HasFilter;
    public bool HasDynamicPermissions;
    // FixedRecordPermissions is not null only if no dynamic permissions and no record filter
    // If not null, this field contains final access rights for all records
    public UserRecordPermission FixedRecordPermissions = new UserRecordPermission();
    // Not null if no dynamic permissions
    public UserEntityTypePermission FixedTypePermissions = new UserEntityTypePermission();
    //Entity set filter
    public QueryPredicate QueryFilterPredicate;

    //Logging
    internal StringBuilder LogBuilder; // creation log, tracks all add/merge actions from original permissions
    internal string Log;

    // Dynamic or record-level permissions
    internal List<CumulativeRecordPermission> ConditionalPermissions = new List<CumulativeRecordPermission>();

    public UserEntityPermissionSet(Type entityType) {
      EntityType = entityType;
      LogBuilder = new StringBuilder();
    }

    public override string ToString() {
      string perms = string.Join(Environment.NewLine, ConditionalPermissions);
      var sDefaultRights = FixedRecordPermissions == null ? "None" : FixedRecordPermissions.ToString();
      var sTypeAccessRights = FixedTypePermissions == null ? "None" : FixedTypePermissions.ToString();
      var result = StringHelper.SafeFormat(
@"  Entity:                       {0}
  DefaultRights:                {1}
  Static table-level Rights:    {2}    (ignores Data filter if any specified)
  HasRecordLevelPermissions:    {3}     
  HasDynamicPermissions:        {4}
............ Granted rights ...............................
{5}
............ Construction log: .................................
{6}
",
       EntityType, sDefaultRights, sTypeAccessRights, HasFilter, HasDynamicPermissions, perms, Log);
      return result;
    }

  }//class



}
