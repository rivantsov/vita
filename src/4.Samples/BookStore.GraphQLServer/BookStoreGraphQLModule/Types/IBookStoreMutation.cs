using System;
using System.Collections.Generic;
using System.Text;

namespace BookStore.GraphQLServer {

  public interface IBookStoreMutation {
    BookOrderLine AddOrderItem(Guid orderId, Guid bookId, int count = 1);
    bool RemoveOrderItem(Guid itemId);
    BookOrder SubmitOrder(Guid orderId);
    BookReview AddReview(BookReviewInput review);
  }
}
