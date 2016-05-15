using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Runtime;
using Vita.Entities.Authorization;

namespace Vita.Entities {

  /// <summary>
  /// Defines the values for <c>ISecureSession.RequireAccessLevel</c> property. This property determines what permission level
  /// (Read or Peek) the current user must have for read operations on entities to succeed. 
  /// </summary>
  public enum ReadAccessLevel {
    /// <summary> Require Peek permission - use it when property values are not intended to be shown to the user directly. 
    /// Use it for entities/properties that are used by application code internally. </summary>
    Peek,     
    
    /// <summary> Require Read permission - use it when property values are intended to be shown to the current user. </summary>
    Read,    
  }

  /// <summary> Defines behavior of the secure entity session for the case when access to the data is denied. </summary>
  public enum DenyReadActionType {
    /// <summary>The system should throw <c>AuthorizationException</c> when user does not have enough permission to access the requested data.</summary>
    Throw, 
    
    /// <summary>Specifies that the system should silently return the type default (null, zero) value for properties, 
    /// null value for a single entity and filtered list for entity lists when access is denied. </summary>
    Filter, 
  }

  /// <summary>Defines a secure session with enabled entity access authorization. </summary>
  /// <remarks>Use <c>IAuthorizationService.OpenSecureSession</c> method to open secure sessions. </remarks>
  public interface ISecureSession : IEntitySession {
    /// <summary> Gets or sets the required read access level for read operations. </summary>
    ReadAccessLevel DemandReadAccessLevel { get; set; }
    
    /// <summary>Gets or sets the action type for denied access.</summary>
    DenyReadActionType DenyReadAction { get; set; }

    /// <summary>Returns a boolean indicating whether action type(s) are allowed for a given entity type, for at least one instance.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="accessType">A flag set representing a set of actions.</param>
    /// <returns>True if action is allowed; otherwise, false.</returns>
    bool IsAccessAllowed<TEntity>(AccessType accessType);
  }

}
