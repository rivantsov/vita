﻿using System;
using System.Collections.Generic;
using System.Text;

namespace BookStore.GraphQL {

  public interface IBookStoreMutation {
    LoginResponse LoginUser(LoginInput login);
    bool Logout(); 
    BookReview AddReview(BookReviewInput review);
    bool DeleteReview(Guid reviewId);
  }
}
