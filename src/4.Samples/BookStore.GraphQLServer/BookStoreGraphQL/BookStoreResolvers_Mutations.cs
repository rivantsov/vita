using System;
using NGraphQL.CodeFirst;
using Vita.Entities;
using Vita.Modules.Login;

namespace BookStore.GraphQL {

  public partial class BookStoreResolvers {


    public IBookReview AddReview(IFieldContext context, BookReviewInput review) {
      var book = _session.GetEntity<IBook>(review.BookId);
      context.AddErrorIf(book == null, "Invalid book Id, book not found.");
      var user = _session.GetEntity<IUser>(review.UserId);
      context.AddErrorIf(user == null, "User not found.");
      context.AddErrorIf(string.IsNullOrWhiteSpace(review.Caption), "Caption may not be empty");
      context.AddErrorIf(string.IsNullOrWhiteSpace(review.Review), "Review text may not be empty");
      context.AddErrorIf(review.Caption != null && review.Caption.Length > 100,
                     "Caption is too long, must be less than 100 chars.");
      context.AddErrorIf(review.Rating < 1 || review.Rating > 5,
                    $"Invalid rating value ({review.Rating}), must be between 1 and 5");
      context.AbortIfErrors();
      // insert review entity
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
