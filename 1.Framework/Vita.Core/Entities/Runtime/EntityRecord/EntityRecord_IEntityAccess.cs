using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Authorization;
using Vita.Entities.Authorization.Runtime;

namespace Vita.Entities.Runtime {

  //Implementation of IEntityAccess authorization interface
  public partial class EntityRecord : IEntityAccess {

    #region IEntityAccess implementation

    public bool IsAuthorizationEnabled {
      get { return Session is SecureSession; }
    } 

    public bool CanPeek<TEntity>(Expression<Func<TEntity, object>> propertySelector) {
      if (!EnsureRecordAccessAssigned()) return true;
      var member = EntityInfo.GetMember(propertySelector);
      return UserPermissions.Peek.Allowed(member);
    }

    public bool CanPeek(string propertyName) {
      if (!EnsureRecordAccessAssigned()) return true;
      var member = EntityInfo.GetMember(propertyName, throwIfNotFound: true);
      return UserPermissions.Peek.Allowed(member);
    }

    public bool CanPeek() {
      if (!EnsureRecordAccessAssigned()) return true;
      return UserPermissions.Peek.Allowed();
    }

    public bool CanRead<TEntity>(Expression<Func<TEntity, object>> propertySelector) {
      if (!EnsureRecordAccessAssigned()) return true;
      var member = EntityInfo.GetMember(propertySelector);
      return UserPermissions.ReadStrict.Allowed(member);
    }

    public bool CanRead(string propertyName) {
      if (!EnsureRecordAccessAssigned()) return true;
      var member = EntityInfo.GetMember(propertyName, throwIfNotFound: true);
      return UserPermissions.ReadStrict.Allowed(member);
    }

    public bool CanRead() {
      if (!EnsureRecordAccessAssigned()) return true;
      return UserPermissions.ReadStrict.Allowed();
    }

    public bool CanUpdate<TEntity>(Expression<Func<TEntity, object>> propertySelector) {
      if (!EnsureRecordAccessAssigned()) return true;
      var member = EntityInfo.GetMember(propertySelector);
      return UserPermissions.UpdateStrict.Allowed(member);
    }

    public bool CanUpdate(string propertyName) {
      if (!EnsureRecordAccessAssigned()) return true;
      var member = EntityInfo.GetMember(propertyName, throwIfNotFound: true);
      return UserPermissions.UpdateStrict.Allowed(member);
    }
    public bool CanUpdate() {
      if (!EnsureRecordAccessAssigned()) return true;
      return UserPermissions.UpdateStrict.Allowed();
    }

    public bool CanDelete() {
      if (!EnsureRecordAccessAssigned()) return true;
      return this.UserPermissions.AccessTypes.IsSet(AccessType.DeleteStrict);
    }

    //private utility method. Checks if record is secured; if yes, verifies that AccessRights information is assigned
    private bool EnsureRecordAccessAssigned() {
      if(UserPermissions != null)
        return true;
      if (Session == null)
        return false;
      var secSession = Session as SecureSession;
      if (secSession == null) {
        UserPermissions = UserRecordPermission.AllowAll;
        return true; 
      }
      if(Session.Context.User.Authority == null)
        return false;
      UserPermissions = Session.Context.User.Authority.GetRecordPermission(this);
      return true;
    }


    #endregion

  }//class
}//ns
