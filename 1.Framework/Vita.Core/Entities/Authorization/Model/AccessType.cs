using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Runtime;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization {

  /// <summary>A flag set representing granular access type (read, write, etc.) for an entity.</summary>
  [Flags]
  public enum AccessType {
    /// <summary>Empty flag set.</summary>
    None,
    
    /// <summary>Restricted read access. Can read internally in code, but user may not see the value. </summary>
    Peek = 1, 

    /// <summary>Can read values and show it to the user in UI.</summary>
    ReadStrict = 1 << 1,

    /// <summary>Can create new entities.</summary>
    CreateStrict = 1 << 2,
    /// <summary>Can update an entity or entities.</summary>
    UpdateStrict = 1 << 3,
    /// <summary>Can delete an entity or entities.</summary>
    DeleteStrict = 1 << 4,

    // Usually the actions come combined: if the user can update the record, most likely he can view it as well. 
    /// <summary>Can read values and show it to the user in UI. Combination of ReadStrict and Peek access.</summary>
    Read = ReadStrict | Peek,
    /// <summary>Can create new and read existing entities.</summary>
    Create = CreateStrict | Read,
    /// <summary>Can view and update an entity or entities.</summary>
    Update = UpdateStrict | Read,
    /// <summary>Can view and delete an entity or entities.</summary>
    Delete = DeleteStrict | Read,
    /// <summary>Full CRUD access - a combination of Create, Read, Update, Delete actions.</summary>
    CRUD = Read | Create | Update | Delete,

    // API access types
    ApiGet = 1 << 8,
    ApiPost = 1 << 9,
    ApiPut = 1 << 10,
    ApiDelete = 1 << 11,

    ApiAll = ApiGet | ApiPost | ApiPut | ApiDelete,
    ApiPutPost = ApiPut | ApiPost, 


    Custom01 = 1 << 16,
    Custom02 = 1 << 17,
    Custom03 = 1 << 18,
    Custom04 = 1 << 19,

  }


}
