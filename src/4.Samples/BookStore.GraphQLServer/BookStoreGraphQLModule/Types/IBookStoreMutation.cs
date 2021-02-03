using System;
using System.Collections.Generic;
using System.Text;

namespace BookStore.GraphQLServer {

  public interface IBookStoreMutation {
    BookReview AddReview(BookReviewInput review);
    bool DeleteReview(Guid reviewId);

    BookOrder GetCart(Guid userId);
    BookOrderLine AddOrderItem(Guid orderId, Guid bookId, int count = 1);
    bool RemoveOrderItem(Guid itemId);
    BookOrder SubmitOrder(Guid orderId);
  }
}
