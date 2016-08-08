using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Authorization;

namespace Vita.Entities {

  /// <summary>Represents user authentication status. </summary>
  public enum UserKind {
    /// <summary>User is not authenticated. </summary>
    Anonymous = 0,
    
    /// <summary>Authenticated user. </summary>
    AuthenticatedUser = 1,
    
    /// <summary>Virtual account, all-powerful user with no restrictions on data access. </summary>
    System = 2,
  }

  public class UserInfo {
    public readonly Guid UserId;
    public readonly string UserName;
    public readonly UserKind Kind;
    /// <summary>Alternative User ID; for applications that use int/identity IDs</summary>
    public readonly Int64 AltUserId;
    internal readonly string Key; //used by some caches and dictionaries; concat of UserId and AltUserId

    public Authority Authority {
      get { return (_authorityDescriptor == null) ? null : _authorityDescriptor.Authority; }
    }

    public UserInfo(Int64 altUserId, string userName) : this(BlankUserId, userName, UserKind.AuthenticatedUser, altUserId) { }
    public UserInfo(Guid userId, string userName) : this(userId, userName, UserKind.AuthenticatedUser, 0) { }

    public UserInfo(Guid userId, string userName, UserKind kind, Int64 altUserId) {
      UserId = userId;
      UserName = userName;
      Kind = kind;
      AltUserId = altUserId;
      Key = GetKey(UserId, AltUserId); 
    }

    public static string GetKey(Guid userId, Int64 altUserId) {
      return userId + "/" + altUserId; 
    }

    public static UserInfo Create(UserKind kind, Guid? userId, Int64? altUserId, string userName) {
      switch(kind) {
        case UserKind.System: return System;
        case UserKind.Anonymous: return Anonymous;
        default:
          Util.Check(!string.IsNullOrWhiteSpace(userName), "UserName may not be empty.");
          Util.Check(userId != null || altUserId != null, "Either UserId or AltUserId must not be null or empty.");
          var guidUserId = userId == null ? BlankUserId : userId.Value;
          var intAltUserId = altUserId == null ? BlankAltUserId : altUserId.Value; 
          return new UserInfo(guidUserId, userName, UserKind.AuthenticatedUser, intAltUserId);
      }
    }

    #region CachedAuthority
    // we use AuthorityDescriptor - a wrapper with Authority + Invalided flag - to be able to invalidate the Authority instances for all UserInfo objects using it. 
    // it happens when we change user roles at runtime - we need to invalidate their Authority objects and force its recalculation.
    public AuthorityDescriptor GetAuthorityDescriptor() {
      return _authorityDescriptor;
    }
    public void SetAuthority(AuthorityDescriptor auth) {
      _authorityDescriptor = auth;
    }
    AuthorityDescriptor _authorityDescriptor;
    #endregion


    //Static singletons for anon and system users
    public static readonly Guid SystemUserId = new Guid("00000000-0000-0000-0000-000000000001");
    public static readonly Guid AnonymousUserId = new Guid("00000000-0000-0000-0000-000000000002");
    public static readonly UserInfo System = new UserInfo(SystemUserId, "%System%", UserKind.System, 1);
    public static readonly UserInfo Anonymous = new UserInfo(AnonymousUserId, "%Anonymous%", UserKind.Anonymous, 0);

    //Used as blank value for UserId when application uses integer AltUserId
    public static readonly Guid BlankUserId = new Guid("00000000-0000-0000-0000-0000000000FF");
    public static readonly int BlankAltUserId = -1;


    public override string ToString() {
      return UserName;
    }
    public override int GetHashCode() {
      return Key.GetHashCode();
    }
  }

}
