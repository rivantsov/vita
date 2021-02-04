using System;
using System.Collections.Generic;
using System.Text;

namespace BookStore.GraphQLServer {

  public interface IBookStoreMutation {
    BookReview AddReview(BookReviewInput review);
    bool DeleteReview(Guid reviewId);
  }
}
