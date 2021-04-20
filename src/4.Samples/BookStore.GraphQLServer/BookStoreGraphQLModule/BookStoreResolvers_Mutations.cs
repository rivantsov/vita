using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.Login;
using Vita.Web;
using NGraphQL.CodeFirst;

namespace BookStore.GraphQLServer {

  public partial class BookStoreResolvers {

    //[ResolvesField("loginUser")] // not needed, auto matched by name
    public LoginResponse LoginUser(IFieldContext context, LoginInput login) {
      // validate input
      context.AddErrorIf(string.IsNullOrEmpty(login.UserName), "UserName may not be empty.");
      context.AddErrorIf(string.IsNullOrEmpty(login.Password), "Password may not be empty.");
      context.AbortIfErrors();
      // try to login
      var opCtx = _session.Context;
      var loginService = _app.GetService<ILoginService>();
      var loginResult = loginService.Login(opCtx, login.UserName, login.Password);
      context.AbortIf(loginResult.Status != LoginAttemptStatus.Success, "Login failed");
      // create Jwt token 
      var tokenCreator = _app.GetService<IAuthenticationTokenHandler>();
      var claims = _app.GetUserClaims(opCtx); // login service sets up user info into context
      var expires = _app.TimeService.UtcNow.AddMinutes(20);
      var token = tokenCreator.CreateToken(claims, expires);
      return new LoginResponse() { Token = token, UserId = opCtx.User.UserId, UserName = opCtx.User.UserName };
    }

    public bool Logout(IFieldContext context) {
      var opCtx = _session.Context;
      if (opCtx.User.Kind != UserKind.AuthenticatedUser)
        return false; 
      var loginService = _app.GetService<ILoginService>();
      loginService.Logout(opCtx);
      return true; 
    }

    public IBookReview AddReview(IFieldContext context, BookReviewInput review) {
      var userInfo = _session.Context.User;
      context.AbortIf(userInfo.Kind != UserKind.AuthenticatedUser, "Only authenticated users can add a review");
      var book = _session.GetEntity<IBook>(review.BookId);
      context.AddErrorIf(book == null, "Invalid book Id, book not found.");
      context.AddErrorIf(userInfo == null, "Invalid user Id, user not found.");
      context.AddErrorIf(string.IsNullOrWhiteSpace(review.Caption), "Caption may not be empty");
      context.AddErrorIf(string.IsNullOrWhiteSpace(review.Review), "Review text may not be empty");
      context.AddErrorIf(review.Caption != null && review.Caption.Length > 100,
                     "Caption is too long, must be less than 100 chars.");
      context.AddErrorIf(review.Rating < 1 || review.Rating > 5,
                    $"Invalid rating value ({review.Rating}), must be between 1 and 5");
      context.AbortIfErrors();
      // Retrieve user and create review
      var user = _session.GetEntity<IUser>(userInfo.UserId);
      var reviewEnt = _session.NewReview(user, book, review.Rating, review.Caption, review.Review);
      // changes will be saved when request completes (see EndRequest method)
      return reviewEnt;
    }

    public bool DeleteReview(IFieldContext context, Guid reviewId) {
      var review = _session.GetEntity<IBookReview>(reviewId);
      context.AbortIf(review == null, "Invalid review ID, review not found.");
      _session.DeleteEntity(review);
      return true;
    }

  }
}
