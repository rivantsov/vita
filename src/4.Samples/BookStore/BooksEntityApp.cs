﻿using System;
using System.Collections.Generic;
using System.Security.Claims;

using Vita.Entities;
using Vita.Entities.DbInfo;
using Vita.Entities.Utilities;

namespace BookStore {

  public class BooksEntityApp : EntityApp {
    public static BooksEntityApp Instance; 

    public const string CurrentVersion = "1.2.1.0";

    public BooksModule MainModule;

    public BooksEntityApp() : base("BookStore", CurrentVersion) {
      //Areas 
      var booksArea = this.AddArea("books");
      var infoArea = this.AddArea("info");
      var loginArea = this.AddArea("login");

      //main module
      MainModule = new BooksModule(booksArea);
      //Standard modules
      var dbInfoModule = new DbInfoModule(infoArea);

      // Replace Guid factory with Sequential Guid generator; we use UUIDNext library
      // Support in .net is coming in .NET 9. See also an example:
      //    https://medium.com/@oliviermartinet/creating-a-uuid-v7-generator-in-c-a95b23cf6a99
      // General case
      EntityApp.GuidFactory = () => UUIDNext.Uuid.NewSequential();
      // SQL Server friendly
      // EntityApp.GuidFactory = () => UUIDNext.Uuid.NewDatabaseFriendly(UUIDNext.Database.SqlServer);


      Instance = this; 
    }

    public override IList<Claim> GetUserClaims(OperationContext userContext) {
      var claims = base.GetUserClaims(userContext);
      var session = userContext.OpenSession();
      using (session.WithElevatedRead()) {
        var userId = userContext.User.UserId; 
        var user = session.GetEntity<IUser>(userId);
        Util.Check(user != null, "User not found, user id: {0}", userId);
        claims.AddRole(user.Type.ToString());
      }
      return claims; 
    }

  }//class
}//ns
